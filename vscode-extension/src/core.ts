// Everything the extension knows about DanaProcessing projects that doesn't
// need VS Code itself: project files, `// nuget:` lines, the reference
// index, the dotnet command lines. Kept free of the `vscode` module so the
// unit tests (node --test) can run it directly.

import * as fs from 'node:fs';
import * as path from 'node:path';

export type Lang = 'en' | 'es';

/** Spanish for any es-* UI language, English for everything else. */
export function langOf(vscodeLanguage: string | undefined): Lang {
  return (vscodeLanguage ?? '').toLowerCase().startsWith('es') ? 'es' : 'en';
}

// ---------------------------------------------------------------------------
// Names

/**
 * A project / folder / class name from anything a person might type or a
 * sample's title: accents dropped, words joined in PascalCase, starting with
 * a letter. "Árbol fractal" -> "ArbolFractal".
 */
export function projectName(text: string, fallback = 'MySketch'): string {
  const plain = text.normalize('NFD').replace(/[̀-ͯ]/g, '');
  const words = plain.split(/[^A-Za-z0-9]+/).filter(Boolean);
  let name = words.map(w => w[0].toUpperCase() + w.slice(1)).join('');
  name = name.replace(/^[0-9]+/, '');
  if (name.length > 48) name = name.slice(0, 48);
  return name.length > 0 ? name : fallback;
}

/** Why a typed project name can't be used, or undefined when it's fine. */
export function projectNameProblem(name: string): 'empty' | 'invalid' | undefined {
  if (name.trim().length === 0) return 'empty';
  if (!/^[A-Za-z_][A-Za-z0-9_.]*$/.test(name)) return 'invalid';
  return undefined;
}

// ---------------------------------------------------------------------------
// `// nuget: Id, Version` lines -- what the DanaProcessing IDE uses to add
// NuGet packages to a sketch. Same pattern as the IDE's PackageDirective.

export interface PackageRef { id: string; version?: string }

const directive = /^\s*\/\/\s*nuget\s*:\s*([A-Za-z0-9_.\-]+)\s*(?:[,\s]\s*([A-Za-z0-9.\-+*]+))?\s*$/;

export function nugetDirectives(source: string): PackageRef[] {
  const found = new Map<string, PackageRef>();
  for (const line of source.split(/\r?\n/)) {
    const m = directive.exec(line);
    if (m) found.set(m[1].toLowerCase(), m[2] ? { id: m[1], version: m[2] } : { id: m[1] });
  }
  return [...found.values()];
}

/** The PackageReference ids a .csproj already has (lower-case). */
export function referencedPackages(csproj: string): Set<string> {
  const ids = new Set<string>();
  for (const m of csproj.matchAll(/<PackageReference\s+Include\s*=\s*"([^"]+)"/gi)) ids.add(m[1].toLowerCase());
  return ids;
}

/**
 * The .csproj with a PackageReference for each package it doesn't reference
 * yet (a new ItemGroup before </Project>). Unchanged if nothing is missing.
 * A package with no version gets "*": the newest stable one.
 */
export function withPackageReferences(csproj: string, packages: PackageRef[]): string {
  const have = referencedPackages(csproj);
  const missing = packages.filter(p => !have.has(p.id.toLowerCase()));
  if (missing.length === 0) return csproj;
  const eol = csproj.includes('\r\n') ? '\r\n' : '\n';
  const lines = missing.map(p => `    <PackageReference Include="${p.id}" Version="${p.version ?? '*'}" />`);
  const group = `  <ItemGroup>${eol}${lines.join(eol)}${eol}  </ItemGroup>${eol}${eol}`;
  const end = csproj.lastIndexOf('</Project>');
  return end < 0 ? csproj : csproj.slice(0, end) + group + csproj.slice(end);
}

// ---------------------------------------------------------------------------
// Recognizing sketches and their projects

/** A class that inherits from Sketch (or AttachedSketch): what the IDE and SketchApp.Run() look for. */
export function looksLikeSketch(source: string): boolean {
  return /\bclass\s+\w+\s*:\s*(?:DanaProcessing\s*\.\s*)?(?:Attached)?Sketch\b/.test(source);
}

/** A .csproj that uses DanaProcessing (the packages, or the projects in this repo). */
export function isDanaProject(csproj: string): boolean {
  return /<(?:PackageReference|ProjectReference)\s+Include\s*=\s*"[^"]*DanaProcessing(?:\.AvaloniaHost)?(?:\.csproj)?"/i.test(csproj);
}

/**
 * The .csproj a file belongs to: the nearest one in its folder or a parent
 * folder, not going above `stopAt` (the workspace folder). One folder with
 * several .csproj files is ambiguous for `dotnet run`, so the first in name
 * order is used.
 */
export function findProjectFile(file: string, stopAt?: string): string | undefined {
  let dir = path.dirname(path.resolve(file));
  const stop = stopAt ? path.resolve(stopAt) : undefined;
  for (;;) {
    let entries: string[] = [];
    try { entries = fs.readdirSync(dir); } catch { /* unreadable: keep climbing */ }
    const projects = entries.filter(e => e.toLowerCase().endsWith('.csproj')).sort();
    if (projects.length > 0) return path.join(dir, projects[0]);
    const parent = path.dirname(dir);
    if (parent === dir || (stop && path.relative(stop, dir) === '')) return undefined;
    if (stop && path.relative(stop, parent).startsWith('..')) return undefined;
    dir = parent;
  }
}

// ---------------------------------------------------------------------------
// Project files

export interface ProjectOptions {
  /** Project, folder and assembly name. */
  name: string;
  /** Version (or range, like "1.*") of DanaProcessing.AvaloniaHost. */
  danaVersion: string;
  /** Sketch.cs. */
  source: string;
  /** Extra packages (from the sketch's `// nuget:` lines). */
  packages?: PackageRef[];
  lang: Lang;
}

export function csprojText(o: Pick<ProjectOptions, 'name' | 'danaVersion' | 'packages' | 'lang'>): string {
  const refs = [{ id: 'DanaProcessing.AvaloniaHost', version: o.danaVersion }, ...(o.packages ?? [])]
    .map(p => `    <PackageReference Include="${p.id}" Version="${p.version ?? '*'}" />`)
    .join('\n');
  const dataComment = o.lang === 'es'
    ? 'Imágenes, fuentes, sonidos... junto al sketch: LoadImage("gato.png")\n         encuentra data/gato.png, también en la app exportada.'
    : 'Images, fonts, sounds... next to the sketch: LoadImage("cat.png")\n         finds data/cat.png, also in the exported app.';
  return `<Project Sdk="Microsoft.NET.Sdk">

  <PropertyGroup>
    <OutputType>WinExe</OutputType>
    <TargetFramework>net10.0</TargetFramework>
    <ImplicitUsings>enable</ImplicitUsings>
    <Nullable>disable</Nullable>
    <RootNamespace>${o.name}</RootNamespace>
  </PropertyGroup>

  <ItemGroup>
${refs}
  </ItemGroup>

  <ItemGroup>
    <!-- ${dataComment} -->
    <None Include="data\\**" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="PreserveNewest" />
  </ItemGroup>

</Project>
`;
}

export function programText(lang: Lang): string {
  return lang === 'es'
    ? `// Abre el sketch en su propia ventana. Sin argumentos, SketchApp.Run()
// encuentra la clase de este proyecto que hereda de Sketch, así que un
// sketch copiado del IDE de DanaProcessing funciona tal cual.
DanaProcessing.AvaloniaHost.SketchApp.Run(args);
`
    : `// Opens the sketch in its own window. With no arguments, SketchApp.Run()
// finds the class in this project that inherits from Sketch, so a sketch
// copied from the DanaProcessing IDE runs as it is.
DanaProcessing.AvaloniaHost.SketchApp.Run(args);
`;
}

/** The starter sketch of "New sketch": colored dots circling the mouse. */
export function starterSketch(className: string, lang: Lang): string {
  const c = lang === 'es'
    ? ['// Un sketch de DanaProcessing: Setup() corre una vez, Draw() en cada cuadro.',
       '// Mueve el mouse; haz clic para cambiar los colores.',
       '// Referencia: https://buddhacodes.github.io/DanaProcessing/reference/a-z.html',
       '// Alrededor del mouse cuando se mueve; del centro hasta entonces.']
    : ['// A DanaProcessing sketch: Setup() runs once, Draw() runs every frame.',
       '// Move the mouse; click to change the colors.',
       '// Reference: https://buddhacodes.github.io/DanaProcessing/reference/a-z.html',
       '// Around the mouse once it moves; around the middle until then.'];
  return `${c[0]}
${c[1]}
${c[2]}

public class ${className} : Sketch
{
    float hue;

    public override void Setup()
    {
        Size(640, 420);
        ColorMode(ColorSpaceMode.HSB, 360, 100, 100);
        NoStroke();
    }

    public override void Draw()
    {
        Background(230, 30, 12);
        ${c[3]}
        float cx = MouseX == 0 && MouseY == 0 ? Width / 2f : MouseX;
        float cy = MouseX == 0 && MouseY == 0 ? Height / 2f : MouseY;
        for (int i = 0; i < 12; i++)
        {
            float t = FrameCount * 0.02f + i * 0.5f;
            float x = cx + Cos(t) * (40 + i * 12);
            float y = cy + Sin(t) * (40 + i * 12);
            Fill((hue + i * 25) % 360, 70, 100);
            Circle(x, y, 26 - i);
        }
    }

    public override void MousePressed() => hue = Random(360);
}
`;
}

export function tasksJson(name: string, lang: Lang): string {
  const live = lang === 'es'
    ? 'Corre el sketch y aplica tus cambios mientras corre (dotnet watch)'
    : 'Runs the sketch and applies your edits while it runs (dotnet watch)';
  return JSON.stringify({
    version: '2.0.0',
    tasks: [
      {
        label: 'build', type: 'process', command: 'dotnet',
        args: ['build', `\${workspaceFolder}/${name}.csproj`],
        problemMatcher: '$msCompile', group: { kind: 'build', isDefault: true },
      },
      {
        label: 'live', detail: live, type: 'process', command: 'dotnet',
        args: ['watch', 'run', '--project', `\${workspaceFolder}/${name}.csproj`],
        isBackground: true, problemMatcher: '$msCompile',
      },
    ],
  }, null, 2) + '\n';
}

export function launchJson(name: string, lang: Lang): string {
  return JSON.stringify({
    version: '0.2.0',
    configurations: [{
      name: lang === 'es' ? 'Depurar el sketch' : 'Debug sketch',
      type: 'coreclr', request: 'launch', preLaunchTask: 'build',
      program: `\${workspaceFolder}/bin/Debug/net10.0/${name}.dll`,
      cwd: '${workspaceFolder}', console: 'internalConsole', stopAtEntry: false,
    }],
  }, null, 2) + '\n';
}

/** Every file of a new sketch project, by path relative to its folder. */
export function projectFiles(o: ProjectOptions): Record<string, string> {
  return {
    [`${o.name}.csproj`]: csprojText(o),
    'Program.cs': programText(o.lang),
    'Sketch.cs': o.source,
    '.vscode/tasks.json': tasksJson(o.name, o.lang),
    '.vscode/launch.json': launchJson(o.name, o.lang),
    '.vscode/extensions.json': JSON.stringify({ recommendations: ['ms-dotnettools.csharp', 'buddhacodes.danaprocessing'] }, null, 2) + '\n',
    '.gitignore': 'bin/\nobj/\nexport/\n',
  };
}

/** Writes the files under `folder` (which must not exist yet, or be empty). */
export function writeProject(folder: string, files: Record<string, string>): void {
  if (fs.existsSync(folder) && fs.readdirSync(folder).length > 0)
    throw new Error(`not empty: ${folder}`);
  for (const [rel, text] of Object.entries(files)) {
    const file = path.join(folder, rel);
    fs.mkdirSync(path.dirname(file), { recursive: true });
    fs.writeFileSync(file, text);
  }
}

// ---------------------------------------------------------------------------
// Examples (examples.json, exported from the IDE's sample catalog)

export interface Example {
  name: { en: string; es: string };
  description: { en: string; es: string };
  tags: { id: string; en: string; es: string }[];
  packages: { id: string; version: string | null }[];
  source: string;
}

export interface LocalizedExample {
  name: string;
  description: string;
  tags: string[];
  packages: PackageRef[];
  source: string;
}

export function localizeExample(e: Example, lang: Lang): LocalizedExample {
  return {
    name: e.name[lang],
    description: e.description[lang],
    tags: e.tags.map(t => t[lang]),
    packages: e.packages.map(p => (p.version ? { id: p.id, version: p.version } : { id: p.id })),
    source: e.source,
  };
}

// ---------------------------------------------------------------------------
// The online reference

export const siteUrl = 'https://buddhacodes.github.io/DanaProcessing/';

export interface ReferenceEntry { href: string; kind: string }

/** The identifier under (or right before) `column` in `line`. */
export function wordAt(line: string, column: number): string | undefined {
  const isWord = (ch: string | undefined) => !!ch && /[A-Za-z0-9_]/.test(ch);
  let start = column, end = column;
  if (!isWord(line[start]) && isWord(line[start - 1])) { start--; end--; }
  if (!isWord(line[start])) return undefined;
  while (start > 0 && isWord(line[start - 1])) start--;
  while (isWord(line[end + 1])) end++;
  const word = line.slice(start, end + 1);
  return /^[A-Za-z_]/.test(word) ? word : undefined;
}

/**
 * The reference page for a name: exact match first, then ignoring case
 * (Processing's `fill` finds `Fill`). Undefined for names it doesn't document.
 */
export function referenceFor(word: string | undefined, index: Record<string, ReferenceEntry>): (ReferenceEntry & { name: string }) | undefined {
  if (!word) return undefined;
  if (index[word]) return { name: word, ...index[word] };
  const lower = word.toLowerCase();
  const name = Object.keys(index).find(k => k.toLowerCase() === lower);
  return name ? { name, ...index[name] } : undefined;
}

// ---------------------------------------------------------------------------
// dotnet command lines

export function runArgs(project: string): string[] {
  return ['run', '--project', project];
}

export function liveArgs(project: string): string[] {
  // --non-interactive: rude edits restart the app instead of asking in the terminal.
  return ['watch', 'run', '--project', project, '--non-interactive'];
}

export interface ExportTarget { rid: string; os: 'windows' | 'macos' | 'linux' }

export const exportTargets: ExportTarget[] = [
  { rid: 'win-x64', os: 'windows' },
  { rid: 'win-arm64', os: 'windows' },
  { rid: 'osx-arm64', os: 'macos' },
  { rid: 'osx-x64', os: 'macos' },
  { rid: 'linux-x64', os: 'linux' },
  { rid: 'linux-arm64', os: 'linux' },
];

/** The export target for the machine VS Code runs on (first in the list). */
export function currentRid(platform: string, arch: string): string {
  const os = platform === 'win32' ? 'win' : platform === 'darwin' ? 'osx' : 'linux';
  return `${os}-${arch === 'arm64' ? 'arm64' : 'x64'}`;
}

/** One self-contained file per platform, in export/<rid>/: runs without .NET installed. */
export function publishArgs(project: string, rid: string, outDir: string): string[] {
  return ['publish', project, '-c', 'Release', '-r', rid, '--self-contained', 'true',
    '-p:PublishSingleFile=true', '-p:IncludeNativeLibrariesForSelfExtract=true', '-o', outDir];
}

export function addPackageArgs(project: string, id: string, version?: string): string[] {
  return ['add', project, 'package', id, ...(version ? ['--version', version] : [])];
}

/** "Id", "Id 1.2.3", "Id@1.2.3" or "Id, 1.2.3" -> a package reference. */
export function parsePackageInput(text: string): PackageRef | undefined {
  const m = /^\s*([A-Za-z0-9_.\-]+)\s*(?:[@,\s]\s*([A-Za-z0-9.\-+*]+))?\s*$/.exec(text);
  if (!m) return undefined;
  return m[2] ? { id: m[1], version: m[2] } : { id: m[1] };
}

// ---------------------------------------------------------------------------
// The DanaProcessing IDE

/** danaide://run?code=... -- the link the IDE registers to open a sketch (base64url UTF-8). */
export function ideUri(source: string): string {
  const code = Buffer.from(source, 'utf8').toString('base64').replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
  return `danaide://run?code=${code}`;
}
