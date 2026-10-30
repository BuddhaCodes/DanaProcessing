import re, html, json
s=open('DanaProcessing.Ide/Editor/SketchSamples.Attached.cs',encoding='utf-8').read()
def sample(name):
    body=s.split('private const string %s =\n@"'%name)[1]
    m=re.search(r'(?<!")"(?!")\s*;', body)
    return body[:m.start()].replace('""','"')

KW=set("public private protected internal class sealed static readonly const override void float int bool char string var new return if else for foreach in while true false null this using namespace continue break".split())
TP=set("Sketch AttachedSketch WindowTarget AttachLayer AttachMode PVector List Math ShellSignals ColorSpaceMode TextAlignH TextAlignV Particle".split())
tok=re.compile(r'(//[^\n]*)|(\$?"(?:[^"\\]|\\.)*"|\'(?:[^\'\\]|\\.)\')|(\b\d+(?:\.\d+)?f?\b)|([A-Za-z_]\w*)|(.)', re.S)
def hl(code):
    out=[]
    for m in tok.finditer(code):
        c,st,num,word,other=m.groups()
        if c: out.append('<span class="cm">%s</span>'%html.escape(c,quote=False))
        elif st: out.append('<span class="str">%s</span>'%html.escape(st,quote=False))
        elif num: out.append('<span class="str">%s</span>'%num)
        elif word:
            if word in KW: out.append('<span class="kw">%s</span>'%word)
            elif word in TP: out.append('<span class="tp">%s</span>'%word)
            else: out.append(word)
        else: out.append(html.escape(other,quote=False))
    return ''.join(out)

snippet_cs=open('DanaProcessing/Sketch/AttachedSketch.Shell.cs',encoding='utf-8').read()
snip=snippet_cs.split('PowerShellSnippet { get; } = BeginMarker + @"')[1].split('" + EndMarker;')[0].replace('""','"')
snip='# >>> DanaProcessing shell signals >>>'+snip+'# <<< DanaProcessing shell signals <<<'

def c(x): return '<code class="inline">%s</code>'%x
def block(code_html, cid=None, run=False):
    idattr=f' id="{cid}"' if cid else ''
    btn=(f'\n  <button class="dana-test-btn" data-target="{cid}" title="Run in DanaProcessing IDE">\n    <i class="ph-bold ph-play"></i>\n    <span class="dana-test-label">Test in Dana</span>\n  </button>') if run else ''
    return f'<div class="code-block">\n  <pre{idattr}>{code_html}</pre>{btn}\n</div>'

page='''<!--meta {"title": "Attached sketches", "nav": "Attached sketches", "summary": "Sketches with no window of their own: they draw on another app's window (Windows Terminal by default), follow it around, feel how it moves and react to its commands.", "tags": []} -->
<p class="section-intro">A sketch with no window of its own: its canvas is another app's window. It follows that window around the screen, takes its size, hides when it's minimized, and can feel how you move it, what you type in it, where you click and whether your last command failed. One sketch per window if you like, slowing down by itself while nobody's looking, and exported as a tray app. Windows only for now. Samples: <em>Rain on the terminal</em>, <em>Water in the terminal</em>, <em>Keyboard bubbles</em>, <em>Flow field in the terminal</em>, <em>Cloth over the whole terminal</em>, <em>Blobs in the terminal</em>, <em>Wobbly functions</em>, <em>CSS blob in the terminal</em> and <em>Audio-reactive terminal</em> (system audio through NAudio).</p>

<h3 class="entry-title" id="attachedsketch">AttachedSketch · Attach(<span class="tp">WindowTarget</span> target, <span class="tp">AttachLayer</span> layer, topInset, <span class="tp">AttachMode</span> mode)</h3>
<p class="entry-desc">Inherit from '''+c('AttachedSketch')+''' instead of '''+c('Sketch')+''' and everything else stays the same: '''+c('Setup()')+''', '''+c('Draw()')+''', Run, ⚡ Hot Reload, ■ Stop. '''+c('Attach()')+''' picks the window to draw on; without it the sketch follows Windows Terminal. '''+c('Width')+'''/'''+c('Height')+''' are the window's (minus '''+c('topInset')+''', the tab bar by default), change when it's resized, and '''+c('WindowResized()')+''' tells you. '''+c('Size()')+''' has no effect. The sketch draws in logical pixels: on a 150 % monitor things keep their size. In the IDE the canvas shows a card saying where the sketch is and what it's doing, with badges for what it uses: <em>N VENTANAS</em>, <em>TECLADO</em> and <em>RATÓN</em> (input hooks, in orange), <em>SHELL</em>, <em>ECO</em>, <em>PAUSA</em>.</p>
<div class="note"><strong>Call Attach() in Settings().</strong> '''+c('Settings()')+''' runs before the window is looked for, so '''+c('Width')+'''/'''+c('Height')+''' are already the target's inside '''+c('Setup()')+'''. Calling '''+c('Attach()')+''' later (a key press, '''+c('Draw()')+''') moves the sketch to the new target on the next frame; '''+c('mode')+''' is only read at the start.</div>
<div class="note"><strong>Clear() every frame.</strong> The canvas starts transparent. '''+c('Clear()')+''' at the start of '''+c('Draw()')+''' keeps the target visible under what you draw; '''+c('Background()')+''' would cover it.</div>

<h3 class="entry-title" id="windowtarget">WindowTarget.Terminal · WindowTarget.Class(className, topInset) · WindowTarget.Process(processName, topInset) · AttachLayer</h3>
<p class="entry-desc">Which window to follow. '''+c('WindowTarget.Terminal')+''' is Windows Terminal (and leaves its 40 px tab bar uncovered). '''+c('WindowTarget.Class("Notepad")')+''' matches a Win32 window class (Spy++ shows it); '''+c('WindowTarget.Process("Code")')+''' the main window of a process, by name, with or without '''+c('.exe')+'''. When several windows match, the one in front wins; when none is open the sketch waits and looks again twice a second. '''+c('AttachLayer.Above')+''' (default) draws on top of the window and lets every click through; '''+c('AttachLayer.Behind')+''' draws right under it, which only shows where the window is see-through (Windows Terminal with <em>opacity</em> below 100, for example) — the text then stays on top.</p>

<h3 class="entry-title" id="each-window">AttachMode.Front · AttachMode.EachWindow · InstanceNumber <span class="sig-return">→ int</span> · MaxInstances</h3>
<p class="entry-desc">'''+c('AttachMode.Front')+''' (default) runs one sketch, on the matching window in front. '''+c('AttachMode.EachWindow')+''' runs one per matching window — every open terminal gets its own, as it opens, up to '''+c('MaxInstances')+''' (8) — and drops it when the window closes. Each window's sketch is a separate instance of your class: fields aren't shared, '''+c('InstanceNumber')+''' is 0, 1, 2…, and each is seeded with '''+c('Seed + InstanceNumber')+''' so '''+c('Random()')+''' and '''+c('Noise()')+''' differ per window. Hot Reload reaches all of them, each keeping its own state; only the first one plays sound.</p>
'''+block(hl('public override void Settings() =>\n    Attach(WindowTarget.Terminal, mode: AttachMode.EachWindow);'))+'''

<h3 class="entry-title" id="target-state">TargetX · TargetY <span class="sig-return">→ int</span> · TargetScale <span class="sig-return">→ float</span> · IsAttached · IsTargetFocused <span class="sig-return">→ bool</span></h3>
<p class="entry-desc">Where the sketch's area is on screen (logical pixels), the monitor's scale (1.5 at 150 %), whether a target is found and showing, and whether it's the window you're typing in. '''+c('MouseX')+'''/'''+c('MouseY')+''' follow the cursor even though clicks go through, and '''+c('MouseMoved()')+''' fires when it moves over the area. '''+c('Focused')+''' is the same as '''+c('IsTargetFocused')+'''.</p>

<h3 class="entry-title" id="target-motion">TargetVelocity · TargetAcceleration <span class="sig-return">→ PVector</span> · IsTargetMoving <span class="sig-return">→ bool</span></h3>
<p class="entry-desc">How the window is moving, in logical px/s and px/s², smoothed so one jittery frame doesn't spike them; acceleration is clamped to ±'''+c('TargetAccelerationLimit')+''' (30000) so a window that jumps across the screen doesn't launch everything into orbit. Things inside a moving box feel the opposite of its acceleration — a glass of water in a car that brakes — so '''+c('-TargetAcceleration.X')+''' is a sideways force and '''+c('Gravity - TargetAcceleration.Y')+''' is gravity: yank the window down and it gets lighter, and things jump. Both are zero while the window is still or hidden. '''+c('IsTargetMoving')+''' is true above '''+c('TargetMovingSpeed')+''' (5 px/s).</p>

<h3 class="entry-title" id="target-events">TargetMoved() · TargetFocused() · TargetBlurred() · TargetShown() · TargetHidden()</h3>
<p class="entry-desc">Override them to react to the window. They run between frames on the same thread as '''+c('Draw()')+''', never in the middle of one, and only for changes after the sketch started (read '''+c('IsTargetFocused')+'''/'''+c('IsAttached')+''' in '''+c('Setup()')+''' for the starting state). '''+c('TargetMoved()')+''' fires every frame the window's position changes. '''+c('TargetHidden()')+''' fires when it's minimized, hidden, moved to another virtual desktop, made too small or closed; '''+c('Draw()')+''' stops until '''+c('TargetShown()')+'''.</p>

<h3 class="entry-title" id="target-input">ListenToTargetKeys(listen) · ListenToTargetMouse(listen)</h3>
<p class="entry-desc">Opt in to the window's input. After '''+c('ListenToTargetKeys()')+''', keys typed into the target reach '''+c('KeyPressed()')+''', '''+c('KeyTyped()')+''' and '''+c('KeyReleased()')+''' with '''+c('Key')+'''/'''+c('KeyCode')+''' set as usual (letters follow Shift and the keyboard layout; arrows and modifiers are '''+c('CODED')+'''). After '''+c('ListenToTargetMouse()')+''', clicks and the wheel over the sketch's area reach '''+c('MousePressed()')+''', '''+c('MouseReleased()')+''', '''+c('MouseClicked()')+''' and '''+c('MouseWheel()')+'''. The target still gets every key and click — nothing is swallowed. Both can be turned off again with '''+c('false')+'''.</p>
<div class="note"><strong>Privacy.</strong> Keys are read only while the target is the foreground window, and clicks only inside the sketch's area when the target itself is under the cursor; nothing typed in any other app ever reaches the sketch. Reading another app's input needs a low-level system hook, which some antivirus tools flag, so it only starts when a sketch asks for it, stops with the sketch, and the IDE's card shows it (<em>TECLADO</em>, <em>RATÓN</em>).</div>

<h3 class="entry-title" id="shell-signals">ListenToShell(listen) · CommandFinished() · OnShellSignal(name) · LastCommandOk <span class="sig-return">→ bool</span> · LastExitCode · CommandCount <span class="sig-return">→ int</span></h3>
<p class="entry-desc">React to the commands you run in the terminal. After '''+c('ListenToShell()')+''', '''+c('CommandFinished()')+''' runs every time a command ends, with '''+c('LastCommandOk')+''' and '''+c('LastExitCode')+''' telling how ('''+c('CommandCount')+''' counts them). An empty Enter doesn't count. '''+c('dana-signal reset')+''' typed in the shell sends a signal of your own: '''+c('OnShellSignal("reset")')+'''. With several terminals, the signal goes to the sketch on the one in front (where you just pressed Enter).</p>
<div class="note"><strong>One-time setup.</strong> The shell has to say how each command ended. <em>Tools → Install PowerShell signals</em> in the IDE (or '''+c('ShellSignals.InstallForPowerShell()')+''', or the same item in an exported app's tray menu) adds a few lines at the end of your '''+c('$PROFILE')+''' — Windows PowerShell's, and PowerShell 7's if you have it, asked to the shells themselves so a moved Documents folder doesn't matter. Open a new terminal afterwards.</div>
<div class="note"><strong>oh-my-posh, starship, posh-git.</strong> Prompt themes replace the prompt when they load, so the lines wrap whatever prompt is active and wrap it again whenever the shell is idle and something replaced it (the same approach as Abyss Tide's hook), handing the theme the same '''+c('$?')+''' and '''+c('$LASTEXITCODE')+''' it would have seen: the theme still shows the error, and the sketch gets it too. Each wrapper keeps the prompt it wraps in a closure, so the chain of prompts can never loop back on itself (an earlier version could, and hung the console after an error), names it with a '''+c('# __PromptWrapper:name')+''' comment, and looks for itself down that chain before wrapping, so two hooks that both re-wrap never wrap each other forever. If it's ever entered twice at once it returns a plain prompt instead of recursing.</div>
<div class="note"><strong>Abyss Tide.</strong> If Abyss Tide's prompt hook is installed, sketches also read its signal file ('''+c('%LOCALAPPDATA%\\AbyssTide\\signal.txt')+''': "ok"/"error", no exit code) — nothing else to install. With both hooks, each command counts once, with Dana's exit code.</div>
<div class="note"><strong>Not reacting?</strong> <em>Tools → Test: failed command</em> (or the tray menu's) sends the signal a failed command would: if the sketch reacts, the sketch side works. Then open a <em>new</em> terminal (open ones don't reload the profile), check that '''+c('Get-EventSubscriber')+''' lists '''+c('PowerShell.OnIdle')+''', run the install again (it updates an older copy), and look at '''+c('Get-Content $env:LOCALAPPDATA\\DanaProcessing\\shell-signal.txt')+''' after a command.</div>
<div class="note"><strong>Privacy.</strong> The lines wrap your existing prompt and write only "ok"/"error", the exit code and the shell's process id to '''+c('%LOCALAPPDATA%\\DanaProcessing\\shell-signal.txt')+''' — never the command itself — and '''+c('ShellSignals.Uninstall(profilePath)')+''' removes them. The IDE's card warns when a sketch listens but the lines aren't installed, or are an older copy.</div>

<h3 class="entry-title" id="shellsignals">ShellSignals.InstallForPowerShell() · Install(profilePath) · Uninstall(profilePath) · IsInstalled(profilePath) · PowerShellProfiles() · PowerShellSnippet · SignalFile</h3>
<p class="entry-desc">The profile lines, and helpers to add or remove them. '''+c('Install')+''' is safe to run again (it replaces its own block, marked by comments, and keeps the rest of the file and its line endings). The block, to paste by hand into '''+c('$PROFILE')+''' if you prefer:</p>
'''+block(html.escape(snip,quote=False))+'''

<h3 class="entry-title" id="eco">DeltaTime <span class="sig-return">→ float</span> · EcoMode(on, fps, whenUnfocused) · NoEco() · IsEco · IsEcoEnabled <span class="sig-return">→ bool</span> · EcoFrameRate <span class="sig-return">→ int</span></h3>
<p class="entry-desc">An attached sketch lives on the desktop all day, so it saves power by itself: while nobody can see it — another window covers the whole target, it isn't moving, and nothing has happened (input, a shell signal, a focus change) for 2 seconds — and whenever a laptop is on battery below 25 %. A terminal that's merely not in front (beside the window you're using, on another monitor) keeps its full frame rate; <code class="inline">EcoMode(whenUnfocused: true)</code> slows it down as soon as it isn't in front, which saves more, it drops to '''+c('EcoFrameRate')+''' (15 by default). Anything happening brings it back to '''+c('FrameRate()')+''' at once. '''+c('IsEco')+''' says when it's slowed down. Use '''+c('DeltaTime')+''' (seconds since the previous frame, at most 0.1) for anything that moves, so motion keeps its speed at any frame rate. '''+c('EcoMode(fps: 5)')+''' changes the slow rate; '''+c('NoEco()')+''' keeps the sketch at full speed always.</p>

<h3 class="entry-title" id="tray-export">Export as a tray app</h3>
<p class="entry-desc"><em>File → Export</em> on an '''+c('AttachedSketch')+''' makes an app with no window of its own: it draws on the window its '''+c('Attach()')+''' picks and lives in the system tray, next to the clock. The icon's menu shows what it's doing (the same text as the IDE's card) and has <em>Pause</em>/<em>Resume</em> (a click on the icon does the same), <em>Install PowerShell signals</em> and <em>Quit</em>. The export dialog says so instead of offering a title bar option.</p>

<h3 class="entry-title" id="tray-install">Install it like Abyss Tide: install.ps1</h3>
<p class="entry-desc">The export dialog of an '''+c('AttachedSketch')+''' has two more options, both on by default. <strong>One sketch per terminal</strong> runs a copy of the sketch on every Windows Terminal window (as if its '''+c('Attach()')+''' said '''+c('AttachMode.EachWindow')+''', whatever it says). <strong>PowerShell installer</strong> puts an <code class="inline">install.ps1</code> and a <code class="inline">LEEME.txt</code> next to the exe. Run it once (right click → Run with PowerShell, or <code class="inline">powershell -ExecutionPolicy Bypass -File .\\install.ps1</code>) and, for the current user and without administrator rights, it copies the app to <code class="inline">%LOCALAPPDATA%\\DanaProcessing\\Sketches\\&lt;name&gt;</code>, unblocks its files, adds the PowerShell prompt hook (so the sketch knows when a command fails), creates a shortcut in the Startup folder and starts it.</p>
<pre class="code-block"><code>.\\install.ps1                      # install, start with Windows, start now
.\\install.ps1 -NoAutostart         # without starting with Windows
.\\install.ps1 -NoShellHook         # without touching your PowerShell profile
.\\install.ps1 -Uninstall           # stop it and remove it
.\\install.ps1 -Uninstall -RemoveShellHook   # also remove the hook (shared by every Dana sketch)</code></pre>
<p class="entry-desc"><strong>In its own Windows Terminal profile</strong> (a third option, on by default, needs the installer) does what Abyss Tide does: <code class="inline">install.ps1</code> adds a <em>PowerShell · &lt;name&gt;</em> profile through a Windows Terminal fragment, and lists it in your <code class="inline">settings.json</code> with a one-line entry like the ones Terminal writes itself (Terminal hides a fragment profile it has seen before but can't find there; a backup goes to <code class="inline">settings.json.bak-dana</code>), see-through at 25 % opacity with a dark scheme of its own, and the sketch draws <em>behind</em> the terminal over a dark backdrop. It shows only in that profile: the other, opaque profiles cover it. Restart Windows Terminal after installing to see the new profile; <code class="inline">-Uninstall</code> removes it. Without this option the sketch draws on top of every terminal.</p>
<div class="note">The exported app runs once per exe: starting it again (autostart plus a double-click, or reinstalling) doesn't put a second overlay on your terminals. The prompt hook is installed by the exe itself (<code class="inline">--install-shell-signals</code>), the same one the IDE's <em>Tools</em> menu installs.</div>

<h3 class="entry-title" id="attached-how">How it works</h3>
<p class="entry-desc">Each sketch runs on a thread of its own with a borderless, click-through window placed exactly over its target, and that thread sleeps until something happens: the next frame is due, or Windows, the input hooks or the shell wake it. Every frame it reads where the target is (DWM's visible bounds, its monitor's DPI, whether it's minimized or cloaked), resizes the surface if needed, runs the events and <code class="inline">Draw()</code>, and puts the pixels on screen: Skia draws straight into the bitmap the window shows, with no copy. Today: Windows only, drawn on the CPU.</p>
<div class="note"><strong>Event-driven, not polled.</strong> Windows reports when the target moves, resizes, minimizes, hides, closes or loses the foreground (WinEvent hooks): a move takes the overlay along at once, between frames, so it doesn't trail a drag (Windows reports hundreds of moves a second while dragging, so they're merged: the overlay moves at most every 4 ms and the sketch keeps drawing frames throughout the drag); the rest wakes the sketch for a frame right away. A key, a click or a shell signal wakes it too — in eco mode as well, so the first event after a quiet spell isn't late. Shell signals are noticed the moment the file is written (a file watcher; a once-a-second poll is only the fallback), and with <code class="inline">AttachMode.EachWindow</code> a terminal you open gets its sketch as soon as it shows. Keyboard and mouse hooks live on another thread, so a slow <code class="inline">Draw()</code> never delays your typing.</div>
<div class="note"><strong>How fast is it?</strong> The IDE's card shows it while the sketch runs: frames per second, what <code class="inline">Draw()</code> and putting the frame on screen cost, and how long the last shell signal, key and window move took to arrive (for example <em>60 fps · dibujo 3.1 ms · presentar 0.9 ms · señal 4 ms · tecla 2 ms · mover 1 ms</em>). For thousands of short lines, draw them in batches (<code class="inline">BeginShape(ShapeKind.Lines)</code>) with <code class="inline">StrokeWeight(0)</code> — a one-pixel hairline, Skia's fastest — as the flow field sample does: it went from 16 to 6 ms a frame.</div>

<h3 class="entry-title" id="attached-example">Runnable example: water in the terminal</h3>
<p class="entry-desc">Water that sloshes when you drag Windows Terminal, splashes when you type in it and dims when you switch away. <strong>Run in Dana</strong> opens it in the IDE; open Windows Terminal before pressing Run. It is also in <em>Sketch → Examples</em>.</p>
'''+block(hl(sample('TerminalWater')),'attached-example-code',True)+'''

<h3 class="entry-title" id="attached-flow-example">Runnable example: a flow field per terminal</h3>
<p class="entry-desc">Particles following a vector field, a different one on each Windows Terminal window. Dragging a terminal throws them back (inertia) and twists the field; a command that fails turns it into a red storm with the exit code, one that works calms it with a cyan pulse. Idea from <a href="https://openprocessing.org/sketch/3017881">"Vector field with particles"</a> by jcponcemath on OpenProcessing, written from scratch for Dana.</p>
'''+block(hl(sample('TerminalFlowField')),'attached-flow-code',True)+'''

<h3 class="entry-title" id="attached-cloth-example">Runnable example: cloth over the terminal</h3>
<p class="entry-desc">A see-through cloth (points and sticks, Verlet integration) covers the whole Windows Terminal window, hanging from its top edge; it swings and folds while you drag the window, because it feels <code class="inline">-TargetAcceleration</code>. The cursor pushes it, a click cuts a hole, a failed command rips one and a command that works sends a gust through it. The cloth is filled as a few big shapes without antialiasing (fast, and neighbouring quads meet with no seams), with a weave of hairlines on top. Idea from <a href="https://openprocessing.org/sketch/2999200">"Cloth!!"</a> by Millie on OpenProcessing, written from scratch for Dana.</p>
'''+block(hl(sample('TerminalCloth')),'attached-cloth-code',True)+'''
'''
open('site/content/attached.html','w',encoding='utf-8').write(page)

kw=json.load(open('site/keywords.json',encoding='utf-8'))
new=[("AttachMode","enum","each-window"),("InstanceNumber","property","each-window"),("MaxInstances","constant","each-window"),
     ("ListenToShell","method","shell-signals"),("CommandFinished","method","shell-signals"),("OnShellSignal","method","shell-signals"),
     ("LastCommandOk","property","shell-signals"),("LastExitCode","property","shell-signals"),("CommandCount","property","shell-signals"),
     ("ShellSignal","struct","shellsignals"),("ShellSignalKind","enum","shellsignals"),("ShellSignals","static class","shellsignals"),("InstallForPowerShell","static method","shellsignals"),
     ("DeltaTime","property","eco"),("EcoMode","method","eco"),("NoEco","method","eco"),("IsEco","property","eco"),("IsEcoEnabled","property","eco"),("EcoFrameRate","property","eco")]
have={k['name'] for k in kw}
for n,k,t in new:
    if n not in have: kw.append({"name":n,"kind":k,"target":t})
open('site/keywords.json','w',encoding='utf-8').write(json.dumps(kw,indent=1,ensure_ascii=False)+"\n")
print(len(page))
