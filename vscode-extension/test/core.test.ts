import { test } from 'node:test';
import * as assert from 'node:assert/strict';
import * as fs from 'node:fs';
import * as os from 'node:os';
import * as path from 'node:path';
import {
  Example, currentRid, findProjectFile, ideUri, isDanaProject, langOf, liveArgs, localizeExample, looksLikeSketch,
  nugetDirectives, parsePackageInput, projectFiles, projectName, projectNameProblem, publishArgs, referenceFor,
  referencedPackages, starterSketch, withPackageReferences, wordAt, writeProject,
} from '../src/core';

const root = path.join(__dirname, '..', '..');

test('UI language: Spanish for es-*, English otherwise', () => {
  assert.equal(langOf('es'), 'es');
  assert.equal(langOf('es-419'), 'es');
  assert.equal(langOf('en-US'), 'en');
  assert.equal(langOf('de'), 'en');
  assert.equal(langOf(undefined), 'en');
});

test('project names from titles', () => {
  assert.equal(projectName('Árbol fractal'), 'ArbolFractal');
  assert.equal(projectName('YT 01 · Hello, Canvas'), 'YT01HelloCanvas');
  assert.equal(projectName('200.000 partículas en la GPU: ruido curl'), 'ParticulasEnLaGPURuidoCurl');
  assert.equal(projectName('¿?'), 'MySketch');
  assert.equal(projectNameProblem('My.Sketch_2'), undefined);
  assert.equal(projectNameProblem('  '), 'empty');
  assert.equal(projectNameProblem('2D sketch'), 'invalid');
});

test('// nuget: lines, as the IDE writes them', () => {
  const src = '// nuget: NAudio, 2.2.1\n//nuget:Rug.Osc 1.2.5\n  // nuget: FlashCap\n// not nuget: X\n// nuget: naudio, 2.2.2';
  assert.deepEqual(nugetDirectives(src), [
    { id: 'naudio', version: '2.2.2' },
    { id: 'Rug.Osc', version: '1.2.5' },
    { id: 'FlashCap' },
  ]);
});

test('missing packages are added to the .csproj, once', () => {
  const csproj = projectFiles({ name: 'A', source: '', danaVersion: '1.*', lang: 'en' })['A.csproj'];
  const once = withPackageReferences(csproj, [{ id: 'NAudio', version: '2.2.1' }, { id: 'danaprocessing.avaloniahost' }, { id: 'FlashCap' }]);
  assert.match(once, /<PackageReference Include="NAudio" Version="2.2.1" \/>/);
  assert.match(once, /<PackageReference Include="FlashCap" Version="\*" \/>/);
  assert.equal(referencedPackages(once).size, 3);
  assert.equal(withPackageReferences(once, [{ id: 'naudio', version: '9' }]), once);
  assert.ok(once.trimEnd().endsWith('</Project>'));
});

test('sketches and Dana projects are recognized', () => {
  assert.ok(looksLikeSketch('public class Bubbles : Sketch {'));
  assert.ok(looksLikeSketch('class X:DanaProcessing.Sketch{}'));
  assert.ok(looksLikeSketch('public class Rain : AttachedSketch'));
  assert.ok(!looksLikeSketch('class X : SketchBook {}'));
  assert.ok(isDanaProject('<PackageReference Include="DanaProcessing.AvaloniaHost" Version="1.*" />'));
  assert.ok(isDanaProject('<PackageReference Include="DanaProcessing" Version="1.0.0" />'));
  assert.ok(isDanaProject('<ProjectReference Include="..\\DanaProcessing\\DanaProcessing.csproj" />'));
  assert.ok(!isDanaProject('<PackageReference Include="DanaProcessingFake" Version="1" />'));
});

test('the nearest .csproj, not above the workspace', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'dana-'));
  try {
    fs.mkdirSync(path.join(dir, 'ws', 'sketch', 'src'), { recursive: true });
    fs.writeFileSync(path.join(dir, 'Outer.csproj'), '');
    fs.writeFileSync(path.join(dir, 'ws', 'sketch', 'B.csproj'), '');
    fs.writeFileSync(path.join(dir, 'ws', 'sketch', 'A.csproj'), '');
    const file = path.join(dir, 'ws', 'sketch', 'src', 'Sketch.cs');
    assert.equal(findProjectFile(file, path.join(dir, 'ws')), path.join(dir, 'ws', 'sketch', 'A.csproj'));
    assert.equal(findProjectFile(path.join(dir, 'ws', 'loose.cs'), path.join(dir, 'ws')), undefined);
    assert.equal(findProjectFile(path.join(dir, 'ws', 'loose.cs')), path.join(dir, 'Outer.csproj'));
  } finally { fs.rmSync(dir, { recursive: true, force: true }); }
});

test('a new project has everything to run, in the UI language', () => {
  for (const lang of ['en', 'es'] as const) {
    const files = projectFiles({ name: 'Bubbles', source: starterSketch('Bubbles', lang), danaVersion: '1.*', packages: [{ id: 'NAudio', version: '2.2.1' }], lang });
    assert.deepEqual(Object.keys(files).sort(), ['.gitignore', '.vscode/extensions.json', '.vscode/launch.json', '.vscode/tasks.json', 'Bubbles.csproj', 'Program.cs', 'Sketch.cs']);
    assert.match(files['Bubbles.csproj'], /Include="DanaProcessing.AvaloniaHost" Version="1\.\*"/);
    assert.match(files['Bubbles.csproj'], /Include="NAudio" Version="2.2.1"/);
    assert.match(files['Program.cs'], /SketchApp\.Run\(args\)/);
    assert.match(files['Sketch.cs'], /public class Bubbles : Sketch/);
    assert.match(files['.vscode/launch.json'], /bin\/Debug\/net10.0\/Bubbles.dll/);
    JSON.parse(files['.vscode/tasks.json']);
  }
  assert.match(starterSketch('X', 'es'), /Mueve el mouse/);
  assert.match(starterSketch('X', 'en'), /Move the mouse/);
});

test('writeProject refuses a folder that is not empty', () => {
  const dir = fs.mkdtempSync(path.join(os.tmpdir(), 'dana-'));
  try {
    writeProject(path.join(dir, 'p'), { 'a/b.txt': 'hi' });
    assert.equal(fs.readFileSync(path.join(dir, 'p', 'a', 'b.txt'), 'utf8'), 'hi');
    assert.throws(() => writeProject(path.join(dir, 'p'), { 'c.txt': '' }));
  } finally { fs.rmSync(dir, { recursive: true, force: true }); }
});

test('examples.json: every example in both languages, runnable as a project', () => {
  const examples: Example[] = JSON.parse(fs.readFileSync(path.join(root, 'examples.json'), 'utf8'));
  assert.ok(examples.length > 50);
  for (const e of examples) {
    for (const lang of ['en', 'es'] as const) {
      const l = localizeExample(e, lang);
      assert.ok(l.name && l.description, `${e.name.en}: name and description in ${lang}`);
      assert.ok(l.tags.every(Boolean));
    }
    assert.ok(looksLikeSketch(e.source), `${e.name.en} has a Sketch class`);
    assert.deepEqual(e.packages.map(p => p.id), nugetDirectives(e.source).map(p => p.id));
    assert.equal(projectNameProblem(projectName(e.name.en)), undefined, e.name.en);
  }
  assert.ok(examples.some(e => e.name.en !== e.name.es), 'names are translated');
});

test('reference lookup: the word under the cursor, any case', () => {
  const index = JSON.parse(fs.readFileSync(path.join(root, 'reference-index.json'), 'utf8'));
  assert.equal(wordAt('    Fill(255, 0, 0);', 6), 'Fill');
  assert.equal(wordAt('    Fill(255, 0, 0);', 8), 'Fill');
  assert.equal(wordAt('    Fill(255, 0, 0);', 2), undefined);
  assert.equal(wordAt('x = 42;', 5), undefined);
  assert.ok(referenceFor('Fill', index)!.href.startsWith('reference/'));
  assert.equal(referenceFor('ellipse', index)!.name, 'Ellipse');
  assert.equal(referenceFor('NotADanaThing', index), undefined);
});

test('dotnet command lines', () => {
  assert.deepEqual(liveArgs('/s/A.csproj').slice(0, 2), ['watch', 'run']);
  const pub = publishArgs('/s/A.csproj', 'osx-arm64', '/s/export/osx-arm64');
  assert.ok(pub.includes('--self-contained') && pub.includes('-p:PublishSingleFile=true'));
  assert.deepEqual(pub.slice(pub.indexOf('-r'), pub.indexOf('-r') + 2), ['-r', 'osx-arm64']);
  assert.equal(currentRid('win32', 'x64'), 'win-x64');
  assert.equal(currentRid('darwin', 'arm64'), 'osx-arm64');
  assert.equal(currentRid('linux', 'x64'), 'linux-x64');
  assert.deepEqual(parsePackageInput('NAudio 2.2.1'), { id: 'NAudio', version: '2.2.1' });
  assert.deepEqual(parsePackageInput('NAudio@2.2.1'), { id: 'NAudio', version: '2.2.1' });
  assert.deepEqual(parsePackageInput('Rug.Osc'), { id: 'Rug.Osc' });
  assert.equal(parsePackageInput('no way / bad'), undefined);
});

test('danaide:// links carry the sketch as base64url', () => {
  const src = 'public class Ñ : Sketch { } // +/?';
  const code = ideUri(src).replace('danaide://run?code=', '');
  assert.ok(!/[+/=]/.test(code));
  assert.equal(Buffer.from(code.replace(/-/g, '+').replace(/_/g, '/'), 'base64').toString('utf8'), src);
});
