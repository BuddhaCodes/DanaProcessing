// DanaProcessing for VS Code: create, run, live-code and export sketches
// with the DanaProcessing NuGet packages, open the IDE's examples, and jump
// to the online reference.

import * as vscode from 'vscode';
import * as fs from 'node:fs';
import * as path from 'node:path';
import * as os from 'node:os';
import { execFile, spawn } from 'node:child_process';
import {
  Example, Lang, LocalizedExample, ReferenceEntry, addPackageArgs, currentRid, exportTargets, findProjectFile,
  ideUri, isDanaProject, langOf, liveArgs, localizeExample, looksLikeSketch, nugetDirectives, parsePackageInput,
  projectFiles, projectName, projectNameProblem, publishArgs, referenceFor, runArgs, siteUrl, starterSketch,
  withPackageReferences, wordAt, writeProject,
} from './core';

const t = vscode.l10n.t;
const TASK_TYPE = 'dana';
const PENDING_OPEN = 'danaProcessing.pendingOpen';

type TaskKind = 'run' | 'live' | 'export' | 'addPackage';
interface DanaTaskDefinition extends vscode.TaskDefinition { kind: TaskKind; project: string; rid?: string }

let ctx: vscode.ExtensionContext;
let lang: Lang = 'en';
let referenceIndex: Record<string, ReferenceEntry> = {};
let examples: Example[] = [];
let runItem: vscode.StatusBarItem;
let liveItem: vscode.StatusBarItem;
let stopItem: vscode.StatusBarItem;

export function activate(context: vscode.ExtensionContext): void {
  ctx = context;
  lang = langOf(vscode.env.language);
  referenceIndex = readJson(context.asAbsolutePath('reference-index.json'), {});

  const commands: [string, (...args: any[]) => unknown][] = [
    ['danaProcessing.newSketch', newSketch],
    ['danaProcessing.openExample', openExample],
    ['danaProcessing.run', (uri?: vscode.Uri) => runProject('run', uri)],
    ['danaProcessing.live', (uri?: vscode.Uri) => runProject('live', uri)],
    ['danaProcessing.stop', stop],
    ['danaProcessing.export', exportSketch],
    ['danaProcessing.addPackage', addPackage],
    ['danaProcessing.makeRunnable', makeRunnable],
    ['danaProcessing.openReference', openReference],
    ['danaProcessing.openInIde', openInIde],
  ];
  for (const [id, handler] of commands)
    context.subscriptions.push(vscode.commands.registerCommand(id, handler));

  context.subscriptions.push(vscode.tasks.registerTaskProvider(TASK_TYPE, {
    provideTasks: () => provideTasks(),
    resolveTask: task => resolveTask(task),
  }));

  context.subscriptions.push(vscode.languages.registerHoverProvider({ language: 'csharp' }, { provideHover }));

  runItem = statusItem('danaProcessing.run', '$(play) Dana', t('Run the sketch'), 102);
  liveItem = statusItem('danaProcessing.live', '$(zap)', t('Live: run the sketch and apply your edits while it runs'), 101);
  stopItem = statusItem('danaProcessing.stop', '$(debug-stop) Dana', t('Stop the sketch'), 100);

  context.subscriptions.push(
    vscode.window.onDidChangeActiveTextEditor(() => updateContext()),
    vscode.workspace.onDidSaveTextDocument(() => updateContext()),
    vscode.workspace.onDidChangeConfiguration(e => { if (e.affectsConfiguration('danaProcessing')) updateContext(); }),
    vscode.tasks.onDidStartTask(() => updateContext()),
    vscode.tasks.onDidEndTask(() => updateContext()),
    vscode.tasks.onDidEndTaskProcess(onTaskProcessEnded),
  );
  updateContext();
  openPendingFile();
}

export function deactivate(): void { /* tasks end with VS Code */ }

// ---------------------------------------------------------------------------
// Settings and small helpers

function config() { return vscode.workspace.getConfiguration('danaProcessing'); }
function dotnet(): string { return config().get<string>('dotnetPath') || 'dotnet'; }

function readJson<T>(file: string, fallback: T): T {
  try { return JSON.parse(fs.readFileSync(file, 'utf8')) as T; } catch { return fallback; }
}

function loadExamples(): Example[] {
  if (examples.length === 0) examples = readJson(ctx.asAbsolutePath('examples.json'), [] as Example[]);
  return examples;
}

function statusItem(command: string, text: string, tooltip: string, priority: number): vscode.StatusBarItem {
  const item = vscode.window.createStatusBarItem(vscode.StatusBarAlignment.Left, priority);
  item.command = command;
  item.text = text;
  item.tooltip = tooltip;
  ctx.subscriptions.push(item);
  return item;
}

/** The project of the active editor's file, if it's a DanaProcessing one. */
function activeDanaProject(): string | undefined {
  const doc = vscode.window.activeTextEditor?.document;
  if (!doc || doc.uri.scheme !== 'file') return undefined;
  return danaProjectFor(doc.uri.fsPath);
}

function danaProjectFor(file: string): string | undefined {
  const root = vscode.workspace.getWorkspaceFolder(vscode.Uri.file(file))?.uri.fsPath;
  const project = findProjectFile(file, root);
  if (!project) return undefined;
  try { return isDanaProject(fs.readFileSync(project, 'utf8')) ? project : undefined; } catch { return undefined; }
}

/** Every DanaProcessing project in the workspace (for the task list and when nothing is open). */
async function workspaceDanaProjects(): Promise<string[]> {
  const files = await vscode.workspace.findFiles('**/*.csproj', '**/{bin,obj,node_modules}/**', 50);
  return files.map(f => f.fsPath).filter(p => {
    try { return isDanaProject(fs.readFileSync(p, 'utf8')); } catch { return false; }
  }).sort();
}

function danaExecutions(kinds?: TaskKind[]): vscode.TaskExecution[] {
  return vscode.tasks.taskExecutions.filter(e =>
    e.task.definition.type === TASK_TYPE && (!kinds || kinds.includes((e.task.definition as DanaTaskDefinition).kind)));
}

function updateContext(): void {
  const doc = vscode.window.activeTextEditor?.document;
  const inProject = !!activeDanaProject();
  const looseSketch = !inProject && !!doc && doc.languageId === 'csharp' && doc.uri.scheme === 'file'
    && looksLikeSketch(doc.getText()) && !findProjectFile(doc.uri.fsPath, vscode.workspace.getWorkspaceFolder(doc.uri)?.uri.fsPath);
  const running = danaExecutions(['run', 'live']).length > 0;
  void vscode.commands.executeCommand('setContext', 'danaProcessing.inProject', inProject);
  void vscode.commands.executeCommand('setContext', 'danaProcessing.looseSketch', looseSketch);
  void vscode.commands.executeCommand('setContext', 'danaProcessing.running', running);

  const show = config().get<boolean>('showStatusBar', true);
  if (show && inProject && !running) { runItem.show(); liveItem.show(); } else { runItem.hide(); liveItem.hide(); }
  if (show && running) stopItem.show(); else stopItem.hide();
}

/** Checks that the .NET SDK is installed; offers the download when it isn't. */
async function ensureDotnet(): Promise<boolean> {
  const ok = await new Promise<boolean>(resolve =>
    execFile(dotnet(), ['--version'], { timeout: 20000 }, (error, stdout) => resolve(!error && /^\d+\./.test(stdout.trim()))));
  if (ok) return true;
  const download = t('Download .NET');
  const choice = await vscode.window.showErrorMessage(
    t('DanaProcessing needs the .NET 10 SDK (the "dotnet" command). Install it and restart VS Code, or set "danaProcessing.dotnetPath".'),
    download);
  if (choice === download) void vscode.env.openExternal(vscode.Uri.parse('https://dotnet.microsoft.com/download/dotnet/10.0'));
  return false;
}

// ---------------------------------------------------------------------------
// New sketch / examples

async function newSketch(): Promise<void> {
  const name = await askProjectName('MySketch');
  if (!name) return;
  await createProject(name, starterSketch(name, lang), []);
}

async function openExample(): Promise<void> {
  const all = loadExamples().map(e => localizeExample(e, lang));
  if (all.length === 0) { void vscode.window.showErrorMessage(t('The examples could not be loaded.')); return; }
  type Item = vscode.QuickPickItem & { example: LocalizedExample };
  const pick = await vscode.window.showQuickPick<Item>(
    all.map(e => ({
      label: e.name,
      description: e.tags.join(' · ') + (e.packages.length ? ' · NuGet: ' + e.packages.map(p => p.id).join(', ') : ''),
      detail: e.description,
      example: e,
    })),
    { title: t('DanaProcessing examples ({0})', all.length), placeHolder: t('Search by name, topic or description'), matchOnDescription: true, matchOnDetail: true });
  if (!pick) return;
  const name = await askProjectName(projectName(pick.example.name));
  if (!name) return;
  await createProject(name, pick.example.source, pick.example.packages);
}

async function askProjectName(suggested: string): Promise<string | undefined> {
  return vscode.window.showInputBox({
    title: t('Sketch name'),
    prompt: t('The name of the project and of its folder.'),
    value: suggested,
    validateInput: value => {
      const problem = projectNameProblem(value);
      if (problem === 'empty') return t('Type a name.');
      if (problem === 'invalid') return t('Use letters, numbers, "_" and "." only, starting with a letter.');
      return undefined;
    },
  });
}

async function createProject(name: string, source: string, packages: { id: string; version?: string }[]): Promise<void> {
  const defaultParent = vscode.workspace.workspaceFolders?.[0]?.uri ?? vscode.Uri.file(os.homedir());
  const parents = await vscode.window.showOpenDialog({
    title: t('Where should the "{0}" folder go?', name),
    openLabel: t('Create here'),
    canSelectFolders: true, canSelectFiles: false, canSelectMany: false,
    defaultUri: defaultParent,
  });
  if (!parents?.[0]) return;
  const folder = path.join(parents[0].fsPath, name);
  if (fs.existsSync(folder) && fs.readdirSync(folder).length > 0) {
    void vscode.window.showErrorMessage(t('"{0}" already exists and isn\'t empty. Choose another name or place.', folder));
    return;
  }
  const files = projectFiles({ name, source, packages, danaVersion: config().get<string>('packageVersion') || '1.*', lang });
  try {
    writeProject(folder, files);
  } catch (e) {
    void vscode.window.showErrorMessage(t('Could not create the project: {0}', String(e)));
    return;
  }
  await openProject(folder, path.join(folder, 'Sketch.cs'));
}

/** Opens a new project folder where the person wants it, then its Sketch.cs. */
async function openProject(folder: string, file: string): Promise<void> {
  const uri = vscode.Uri.file(folder);
  let where = config().get<string>('openProjectIn', 'ask');
  if (where === 'ask') {
    const items = [
      { label: t('Open in this window'), value: 'currentWindow' },
      { label: t('Open in a new window'), value: 'newWindow' },
      { label: t('Add to this workspace'), value: 'addToWorkspace' },
    ];
    const pick = await vscode.window.showQuickPick(items, { title: t('Sketch created: {0}', path.basename(folder)) });
    if (!pick) { await vscode.window.showTextDocument(vscode.Uri.file(file)); return; }
    where = pick.value;
  }
  if (where === 'addToWorkspace') {
    vscode.workspace.updateWorkspaceFolders(vscode.workspace.workspaceFolders?.length ?? 0, 0, { uri });
    await vscode.window.showTextDocument(vscode.Uri.file(file));
    return;
  }
  // The window reloads with the folder: Sketch.cs is opened on the next activation.
  await ctx.globalState.update(PENDING_OPEN, file);
  await vscode.commands.executeCommand('vscode.openFolder', uri, { forceNewWindow: where === 'newWindow' });
}

function openPendingFile(): void {
  const file = ctx.globalState.get<string>(PENDING_OPEN);
  if (!file) return;
  const inThisWindow = vscode.workspace.workspaceFolders?.some(f => !path.relative(f.uri.fsPath, file).startsWith('..'));
  if (!inThisWindow) return;
  void ctx.globalState.update(PENDING_OPEN, undefined);
  if (fs.existsSync(file)) void vscode.window.showTextDocument(vscode.Uri.file(file));
}

// ---------------------------------------------------------------------------
// Run / live / stop

function makeTask(definition: DanaTaskDefinition, args: string[], name: string, background: boolean): vscode.Task {
  const folder = vscode.workspace.getWorkspaceFolder(vscode.Uri.file(definition.project)) ?? vscode.TaskScope.Workspace;
  const task = new vscode.Task(definition, folder, name, 'DanaProcessing',
    new vscode.ProcessExecution(dotnet(), args, { cwd: path.dirname(definition.project) }), '$msCompile');
  task.isBackground = background;
  task.presentationOptions = { reveal: vscode.TaskRevealKind.Always, panel: vscode.TaskPanelKind.Dedicated, clear: true, showReuseMessage: false };
  if (definition.kind === 'run' || definition.kind === 'live') task.group = vscode.TaskGroup.Test;
  return task;
}

function taskFor(kind: 'run' | 'live', project: string): vscode.Task {
  const name = path.basename(project, '.csproj');
  return kind === 'run'
    ? makeTask({ type: TASK_TYPE, kind, project }, runArgs(project), t('Run {0}', name), false)
    : makeTask({ type: TASK_TYPE, kind, project }, liveArgs(project), t('Live {0}', name), true);
}

async function provideTasks(): Promise<vscode.Task[]> {
  const projects = await workspaceDanaProjects();
  return projects.flatMap(p => [taskFor('run', p), taskFor('live', p)]);
}

function resolveTask(task: vscode.Task): vscode.Task | undefined {
  const d = task.definition as DanaTaskDefinition;
  if ((d.kind === 'run' || d.kind === 'live') && d.project) {
    const project = path.isAbsolute(d.project) ? d.project
      : path.join((task.scope as vscode.WorkspaceFolder | undefined)?.uri?.fsPath ?? '', d.project);
    const resolved = taskFor(d.kind, project);
    return new vscode.Task(d, task.scope ?? vscode.TaskScope.Workspace, resolved.name, 'DanaProcessing', resolved.execution, '$msCompile');
  }
  return undefined;
}

/** The project to act on: the clicked file's, the active editor's, or the only/picked one in the workspace. */
async function pickProject(uri?: vscode.Uri): Promise<string | undefined> {
  if (uri?.scheme === 'file') {
    if (uri.fsPath.endsWith('.csproj')) return uri.fsPath;
    const p = danaProjectFor(uri.fsPath);
    if (p) return p;
  }
  const active = activeDanaProject();
  if (active) return active;

  const doc = vscode.window.activeTextEditor?.document;
  if (doc?.languageId === 'csharp' && looksLikeSketch(doc.getText()) && !findProjectFile(doc.uri.fsPath)) {
    const make = t('Make it runnable');
    const choice = await vscode.window.showInformationMessage(
      t('This sketch isn\'t in a project yet, so it can\'t run from VS Code. Create a project for it?'), make);
    if (choice === make) await makeRunnable();
    return undefined;
  }

  const projects = await workspaceDanaProjects();
  if (projects.length === 1) return projects[0];
  if (projects.length > 1) {
    const pick = await vscode.window.showQuickPick(projects.map(p => ({ label: path.basename(p, '.csproj'), description: vscode.workspace.asRelativePath(p), project: p })),
      { title: t('Which sketch?') });
    return pick?.project;
  }
  const create = t('New sketch');
  const example = t('Open an example');
  const choice = await vscode.window.showInformationMessage(t('There\'s no DanaProcessing sketch here yet.'), create, example);
  if (choice === create) await newSketch();
  if (choice === example) await openExample();
  return undefined;
}

async function runProject(kind: 'run' | 'live', uri?: vscode.Uri): Promise<void> {
  const project = await pickProject(uri);
  if (!project || !(await ensureDotnet())) return;
  await vscode.workspace.saveAll(false);
  await syncNugetDirectives(project);
  // One sketch at a time per project: a new run replaces the old one.
  for (const e of danaExecutions(['run', 'live']))
    if ((e.task.definition as DanaTaskDefinition).project === project) e.terminate();
  await vscode.tasks.executeTask(taskFor(kind, project));
}

function stop(): void {
  const running = danaExecutions(['run', 'live']);
  if (running.length === 0) { void vscode.window.setStatusBarMessage(t('No sketch is running.'), 3000); return; }
  for (const e of running) e.terminate();
}

/**
 * Sketches from the IDE add packages with `// nuget: Id, Version` lines;
 * here those become PackageReferences in the .csproj before each run.
 */
async function syncNugetDirectives(project: string): Promise<void> {
  const folder = path.dirname(project);
  const files = await vscode.workspace.findFiles(new vscode.RelativePattern(folder, '**/*.cs'), new vscode.RelativePattern(folder, '{bin,obj}/**'), 200);
  const packages = files.flatMap(f => { try { return nugetDirectives(fs.readFileSync(f.fsPath, 'utf8')); } catch { return []; } });
  if (packages.length === 0) return;
  const before = fs.readFileSync(project, 'utf8');
  const after = withPackageReferences(before, packages);
  if (after === before) return;
  fs.writeFileSync(project, after);
  const added = packages.filter(p => !before.toLowerCase().includes(`include="${p.id.toLowerCase()}"`)).map(p => p.id);
  void vscode.window.setStatusBarMessage(t('Added to {0}: {1}', path.basename(project), added.join(', ')), 6000);
}

// ---------------------------------------------------------------------------
// Export

async function exportSketch(uri?: vscode.Uri): Promise<void> {
  const project = await pickProject(uri);
  if (!project || !(await ensureDotnet())) return;
  const here = currentRid(process.platform, process.arch);
  const osName = { windows: 'Windows', macos: 'macOS', linux: 'Linux' };
  const items = [...exportTargets].sort((a, b) => Number(b.rid === here) - Number(a.rid === here)).map(target => ({
    label: `${osName[target.os]} (${target.rid.split('-')[1]})`,
    description: target.rid === here ? t('this computer') : target.rid,
    picked: target.rid === here,
    rid: target.rid,
  }));
  const picks = await vscode.window.showQuickPick(items, {
    title: t('Export {0} as an app', path.basename(project, '.csproj')),
    placeHolder: t('Pick the systems to export for. Each one is a single file that runs without installing .NET.'),
    canPickMany: true,
  });
  if (!picks || picks.length === 0) return;
  await vscode.workspace.saveAll(false);
  await syncNugetDirectives(project);
  for (const pick of picks) {
    const out = path.join(path.dirname(project), 'export', pick.rid);
    await vscode.tasks.executeTask(makeTask({ type: TASK_TYPE, kind: 'export', project, rid: pick.rid },
      publishArgs(project, pick.rid, out), t('Export {0} ({1})', path.basename(project, '.csproj'), pick.rid), false));
  }
}

function onTaskProcessEnded(e: vscode.TaskProcessEndEvent): void {
  const d = e.execution.task.definition as DanaTaskDefinition;
  if (d.type !== TASK_TYPE) return;
  if (d.kind === 'export') {
    const out = path.join(path.dirname(d.project), 'export', d.rid ?? '');
    if (e.exitCode === 0) {
      const reveal = t('Show the folder');
      void vscode.window.showInformationMessage(t('Exported for {0}: {1}', d.rid ?? '', out), reveal).then(choice => {
        if (choice === reveal) void vscode.commands.executeCommand('revealFileInOS', vscode.Uri.file(out));
      });
    } else {
      void vscode.window.showErrorMessage(t('The export for {0} failed. The terminal shows why.', d.rid ?? ''));
    }
  }
  if (d.kind === 'addPackage' && e.exitCode !== 0)
    void vscode.window.showErrorMessage(t('The package could not be added. The terminal shows why.'));
}

// ---------------------------------------------------------------------------
// Packages

async function addPackage(uri?: vscode.Uri): Promise<void> {
  const project = await pickProject(uri);
  if (!project) return;
  const text = await vscode.window.showInputBox({
    title: t('Add a NuGet package to {0}', path.basename(project, '.csproj')),
    prompt: t('The package id, and a version if you want a specific one: "NAudio" or "NAudio 2.2.1".'),
    placeHolder: 'NAudio 2.2.1',
    validateInput: value => (parsePackageInput(value) ? undefined : t('That doesn\'t look like a package id.')),
  });
  const ref = text ? parsePackageInput(text) : undefined;
  if (!ref || !(await ensureDotnet())) return;
  await vscode.tasks.executeTask(makeTask({ type: TASK_TYPE, kind: 'addPackage', project },
    addPackageArgs(project, ref.id, ref.version), t('Add {0}', ref.id), false));
}

// ---------------------------------------------------------------------------
// A loose sketch file (saved from the IDE) -> a project

async function makeRunnable(uri?: vscode.Uri): Promise<void> {
  const file = uri?.scheme === 'file' ? uri.fsPath : vscode.window.activeTextEditor?.document.uri.fsPath;
  if (!file || !file.endsWith('.cs')) { void vscode.window.showErrorMessage(t('Open a sketch (.cs file) first.')); return; }
  await vscode.workspace.saveAll(false);
  const source = fs.readFileSync(file, 'utf8');
  if (!looksLikeSketch(source)) { void vscode.window.showErrorMessage(t('This file has no class that inherits from Sketch.')); return; }
  const existing = findProjectFile(file);
  if (existing) { void vscode.window.showInformationMessage(t('This sketch is already in a project: {0}', existing)); return; }

  const dir = path.dirname(file);
  const name = projectName(path.basename(file, '.cs'), 'Sketch');
  const danaVersion = config().get<string>('packageVersion') || '1.*';
  const files = projectFiles({ name, source, packages: nugetDirectives(source), danaVersion, lang });
  const otherCode = fs.readdirSync(dir).filter(f => f.endsWith('.cs') && path.join(dir, f) !== file);

  if (otherCode.length === 0) {
    // Alone in its folder (with its data/, if any): the project goes right here.
    delete files['Sketch.cs'];
    for (const [rel, text] of Object.entries(files)) {
      const target = path.join(dir, rel);
      if (fs.existsSync(target)) continue;
      fs.mkdirSync(path.dirname(target), { recursive: true });
      fs.writeFileSync(target, text);
    }
    void vscode.window.showInformationMessage(t('{0} is ready: run it with the ▶ button.', name));
    updateContext();
    return;
  }
  // Other .cs files around it would all end up in the project: use a folder of its own.
  const folder = path.join(dir, name);
  try { writeProject(folder, files); } catch { void vscode.window.showErrorMessage(t('"{0}" already exists and isn\'t empty. Choose another name or place.', folder)); return; }
  const data = path.join(dir, 'data');
  if (fs.existsSync(data)) fs.cpSync(data, path.join(folder, 'data'), { recursive: true });
  await openProject(folder, path.join(folder, 'Sketch.cs'));
}

// ---------------------------------------------------------------------------
// Reference

function referenceAtCursor(): (ReferenceEntry & { name: string }) | undefined {
  const editor = vscode.window.activeTextEditor;
  if (!editor) return undefined;
  const pos = editor.selection.active;
  const selected = editor.document.getText(editor.selection).trim();
  const word = selected && /^\w+$/.test(selected) ? selected : wordAt(editor.document.lineAt(pos.line).text, pos.character);
  return referenceFor(word, referenceIndex);
}

async function openReference(): Promise<void> {
  const entry = referenceAtCursor();
  if (entry) { await vscode.env.openExternal(vscode.Uri.parse(siteUrl + entry.href)); return; }
  type Item = vscode.QuickPickItem & { href: string };
  const items: Item[] = Object.entries(referenceIndex).map(([name, e]) => ({ label: name, description: e.kind, href: e.href }));
  items.unshift({ label: t('A-Z: everything in DanaProcessing'), description: '', href: 'reference/a-z.html', alwaysShow: true });
  const pick = await vscode.window.showQuickPick(items, { title: t('DanaProcessing reference'), placeHolder: t('Search a function, class or property') });
  if (pick) await vscode.env.openExternal(vscode.Uri.parse(siteUrl + pick.href));
}

function provideHover(document: vscode.TextDocument, position: vscode.Position): vscode.Hover | undefined {
  if (!config().get<boolean>('referenceHover', true) || document.uri.scheme !== 'file') return undefined;
  const range = document.getWordRangeAtPosition(position, /[A-Za-z_][A-Za-z0-9_]*/);
  if (!range) return undefined;
  const entry = referenceIndex[document.getText(range)];   // exact name only: hovers stay quiet on everything else
  if (!entry || !danaProjectFor(document.uri.fsPath)) return undefined;
  const md = new vscode.MarkdownString(`$(book) [${t('{0} in the DanaProcessing reference', document.getText(range))}](${siteUrl}${entry.href})`, true);
  md.isTrusted = false;
  return new vscode.Hover(md, range);
}

// ---------------------------------------------------------------------------
// The DanaProcessing IDE

async function openInIde(): Promise<void> {
  const doc = vscode.window.activeTextEditor?.document;
  if (!doc || doc.languageId !== 'csharp') { void vscode.window.showErrorMessage(t('Open a sketch (.cs file) first.')); return; }
  const link = ideUri(doc.getText());
  const idePath = config().get<string>('idePath');
  if (idePath) {
    try {
      spawn(idePath, [link], { detached: true, stdio: 'ignore' }).unref();
      return;
    } catch (e) {
      void vscode.window.showErrorMessage(t('Could not start the IDE at "{0}": {1}', idePath, String(e)));
      return;
    }
  }
  if (process.platform === 'win32') {
    // The IDE registers danaide:// for the current user the first time it runs.
    if (await vscode.env.openExternal(vscode.Uri.parse(link, true))) return;
  }
  const setPath = t('Set the IDE path');
  const download = t('Download the IDE');
  const choice = await vscode.window.showInformationMessage(
    t('To open sketches in the DanaProcessing IDE, set "danaProcessing.idePath" to where it is installed.'), setPath, download);
  if (choice === setPath) void vscode.commands.executeCommand('workbench.action.openSettings', 'danaProcessing.idePath');
  if (choice === download) void vscode.env.openExternal(vscode.Uri.parse(siteUrl));
}
