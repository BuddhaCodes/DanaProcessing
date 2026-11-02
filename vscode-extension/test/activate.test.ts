// Activation against a stand-in `vscode` module: every command package.json
// declares is registered, and the status bar follows the active file.
import { test } from 'node:test';
import * as assert from 'node:assert/strict';
import * as fs from 'node:fs';
import * as os from 'node:os';
import * as path from 'node:path';
import Module = require('node:module');

const root = path.join(__dirname, '..', '..');
const registered: string[] = [];
const contexts = new Map<string, unknown>();
const items: { text: string; visible: boolean }[] = [];
let activeEditor: unknown;
const noop = () => ({ dispose() { /* */ } });

const fake = {
  l10n: { t: (s: string, ...args: unknown[]) => s.replace(/\{(\d)\}/g, (_, i) => String(args[+i])) },
  env: { language: 'es' },
  commands: {
    registerCommand: (id: string) => { registered.push(id); return noop(); },
    executeCommand: async (id: string, key: string, value: unknown) => { if (id === 'setContext') contexts.set(key, value); },
  },
  tasks: { registerTaskProvider: noop, onDidStartTask: noop, onDidEndTask: noop, onDidEndTaskProcess: noop, taskExecutions: [] },
  languages: { registerHoverProvider: noop },
  window: {
    createStatusBarItem: () => { const item = { text: '', visible: false, show() { item.visible = true; }, hide() { item.visible = false; }, dispose() { /* */ } }; items.push(item); return item; },
    onDidChangeActiveTextEditor: noop,
    get activeTextEditor() { return activeEditor; },
  },
  workspace: {
    onDidSaveTextDocument: noop, onDidChangeConfiguration: noop,
    getConfiguration: () => ({ get: (_k: string, d?: unknown) => d }),
    getWorkspaceFolder: () => undefined,
    workspaceFolders: undefined,
  },
  StatusBarAlignment: { Left: 1 },
  Uri: { file: (p: string) => ({ fsPath: p, scheme: 'file' }) },
};

const load = (Module as any)._load;
(Module as any)._load = function (request: string, ...rest: unknown[]) {
  return request === 'vscode' ? fake : load.call(this, request, ...rest);
};

test('activation registers every declared command and tracks the active sketch', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'dana-'));
  try {
    fs.writeFileSync(path.join(dir, 'S.csproj'), '<Project><ItemGroup><PackageReference Include="DanaProcessing.AvaloniaHost" Version="1.*" /></ItemGroup></Project>');
    const file = path.join(dir, 'Sketch.cs');
    fs.writeFileSync(file, 'public class S : Sketch { }');
    activeEditor = { document: { uri: { fsPath: file, scheme: 'file' }, languageId: 'csharp', getText: () => 'public class S : Sketch { }' } };

    const ext = require('../src/extension');
    const state = new Map<string, unknown>();
    ext.activate({ subscriptions: [], asAbsolutePath: (p: string) => path.join(root, p), globalState: { get: (k: string) => state.get(k), update: async (k: string, v: unknown) => { state.set(k, v); } } });

    const declared = JSON.parse(fs.readFileSync(path.join(root, 'package.json'), 'utf8')).contributes.commands.map((c: { command: string }) => c.command);
    assert.deepEqual([...registered].sort(), [...declared].sort());
    assert.equal(contexts.get('danaProcessing.inProject'), true);
    assert.equal(contexts.get('danaProcessing.running'), false);
    assert.deepEqual(items.map(i => i.visible), [true, true, false]);   // Run, Live shown; Stop hidden
  } finally { fs.rmSync(dir, { recursive: true, force: true }); }
});
