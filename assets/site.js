// DanaProcessing docs: menu, search, copy / Run in Dana, A–Z filter, downloads.
// Everything here is an enhancement: without JavaScript every page still
// reads, every link works and the menus simply stay open.
(function () {
  var html = document.documentElement;
  html.classList.add("js");
  var ROOT = html.getAttribute("data-root") || "./";

  // ---------- Disclosure buttons: the site menu (phones) and the topic list ----------
  function wireToggle(button, panel) {
    if (!button || !panel) return;
    button.addEventListener("click", function () {
      var open = button.getAttribute("aria-expanded") !== "true";
      button.setAttribute("aria-expanded", String(open));
      panel.classList.toggle("open", open);
    });
  }
  wireToggle(document.querySelector(".menu-toggle"), document.getElementById("site-menu"));
  wireToggle(document.querySelector(".topics-toggle"), document.getElementById("topics"));

  // ---------- Search ----------
  var input = document.getElementById("site-search");
  var results = document.getElementById("search-results");
  var status = document.getElementById("search-status");

  function norm(s) { return s.toLowerCase().normalize("NFD").replace(/[̀-ͯ]/g, ""); }

  function search(q) {
    var index = window.DANA_INDEX || [];
    q = norm(q.trim());
    if (!q) return [];
    var scored = [];
    for (var i = 0; i < index.length; i++) {
      var e = index[i];
      var name = norm(e[0]);
      var score = name === q ? 0 : name.indexOf(q) === 0 ? 1 : name.indexOf(q) > 0 ? 2 : norm(e[3]).indexOf(q) >= 0 ? 3 : -1;
      if (score >= 0) scored.push([score, e[2] === "topic" ? 0 : 1, e[0].length, e]);
    }
    scored.sort(function (a, b) { return a[0] - b[0] || a[1] - b[1] || a[2] - b[2]; });
    return scored.slice(0, 12).map(function (s) { return s[3]; });
  }

  function render() {
    var q = input.value;
    results.innerHTML = "";
    if (!q.trim()) {
      results.hidden = true;
      status.textContent = "";
      return;
    }
    var found = search(q);
    found.forEach(function (e) {
      var li = document.createElement("li");
      var a = document.createElement("a");
      a.href = ROOT + e[1];
      var name = document.createElement("span");
      name.className = "r-name";
      name.textContent = e[0];
      var where = document.createElement("span");
      where.className = "r-where";
      where.textContent = e[2] === "topic" ? "Topic · " + e[3] : (e[2] ? e[2] + " · " : "") + e[3];
      a.appendChild(name);
      a.appendChild(where);
      li.appendChild(a);
      results.appendChild(li);
    });
    if (!found.length) {
      var li = document.createElement("li");
      li.className = "r-empty";
      li.textContent = "Nothing matches “" + q + "”. Try a function name, like Fill or Beat.";
      results.appendChild(li);
    }
    results.hidden = false;
    status.textContent = found.length ? found.length + (found.length === 1 ? " result" : " results") : "No results";
  }

  if (input && results) {
    input.addEventListener("input", render);
    input.addEventListener("keydown", function (e) {
      if (e.key === "Escape") { input.value = ""; render(); }
      else if (e.key === "ArrowDown") {
        var first = results.querySelector("a");
        if (first) { e.preventDefault(); first.focus(); }
      } else if (e.key === "Enter") {
        var top = results.querySelector("a");
        if (top) { e.preventDefault(); location.href = top.href; }
      }
    });
    results.addEventListener("keydown", function (e) {
      var links = Array.prototype.slice.call(results.querySelectorAll("a"));
      var i = links.indexOf(document.activeElement);
      if (e.key === "ArrowDown" && i < links.length - 1) { e.preventDefault(); links[i + 1].focus(); }
      else if (e.key === "ArrowUp") { e.preventDefault(); (i > 0 ? links[i - 1] : input).focus(); }
      else if (e.key === "Escape") { input.focus(); input.value = ""; render(); }
    });
    document.addEventListener("click", function (e) {
      if (!e.target.closest || !e.target.closest(".search")) results.hidden = true;
    });
    input.addEventListener("focus", function () { if (input.value.trim()) results.hidden = false; });
    // "/" jumps to search, unless you're typing somewhere.
    document.addEventListener("keydown", function (e) {
      if (e.key !== "/" || e.ctrlKey || e.metaKey || e.altKey) return;
      var t = e.target;
      if (t && (t.isContentEditable || /^(INPUT|TEXTAREA|SELECT)$/.test(t.tagName))) return;
      e.preventDefault();
      var menu = document.getElementById("site-menu");
      var toggle = document.querySelector(".menu-toggle");
      if (menu && toggle && getComputedStyle(toggle).display !== "none" && !menu.classList.contains("open")) toggle.click();
      input.focus();
    });
  }

  // ---------- Reels: muted, looping, never forced on anyone ----------
  // Without JavaScript the video just has its own controls. With it: it
  // plays by itself only if the system doesn't ask for reduced motion, a
  // "Pause" button is always there (anything moving for more than 5 s must
  // be stoppable), and it rests while scrolled out of view.
  var reduceMotion = window.matchMedia && window.matchMedia("(prefers-reduced-motion: reduce)").matches;
  document.querySelectorAll("video[data-autoplay]").forEach(function (video) {
    var button = video.parentElement.querySelector(".reel-toggle");
    if (!button) return;
    video.removeAttribute("controls");
    button.hidden = false;
    var userPaused = reduceMotion;
    var sync = function () { button.textContent = video.paused ? "▶ Play" : "❚❚ Pause"; };
    var play = function () { var p = video.play(); if (p && p.catch) p.catch(function () { sync(); }); };
    video.addEventListener("play", sync);
    video.addEventListener("pause", sync);
    button.addEventListener("click", function () {
      if (video.paused) { userPaused = false; play(); } else { userPaused = true; video.pause(); }
    });
    if ("IntersectionObserver" in window) {
      new IntersectionObserver(function (entries) {
        entries.forEach(function (e) {
          if (e.isIntersecting && !userPaused) play();
          else if (!e.isIntersecting && !video.paused) video.pause();
        });
      }, { threshold: 0.25 }).observe(video);
    } else if (!userPaused) {
      play();
    }
    sync();
  });

  // ---------- Analytics: only with permission ----------
  // The page carries the measurement id but not Google's script. The script
  // loads only once the visitor says yes; the answer is remembered in this
  // browser and can be changed any time from "Privacy choices" in the
  // footer. Browsers sending Global Privacy Control or Do Not Track are
  // taken as "no" and not asked. Without JavaScript nothing loads at all.
  var analyticsMeta = document.querySelector('meta[name="dana-analytics"]');
  var GA_ID = analyticsMeta && analyticsMeta.getAttribute("content");
  var CONSENT_KEY = "dana-analytics-consent";
  var readConsent = function () { try { return localStorage.getItem(CONSENT_KEY); } catch (e) { return null; } };
  var saveConsent = function (v) { try { localStorage.setItem(CONSENT_KEY, v); } catch (e) { /* private mode: ask again next time */ } };
  var privacySignal = function () {
    return navigator.globalPrivacyControl === true || navigator.doNotTrack === "1" || window.doNotTrack === "1";
  };
  var analyticsLoaded = false;
  var loadAnalytics = function () {
    if (!GA_ID) return;
    window["ga-disable-" + GA_ID] = false;
    if (analyticsLoaded) return;
    analyticsLoaded = true;
    window.dataLayer = window.dataLayer || [];
    window.gtag = function () { window.dataLayer.push(arguments); };
    window.gtag("js", new Date());
    window.gtag("config", GA_ID);
    var s = document.createElement("script");
    s.async = true;
    s.src = "https://www.googletagmanager.com/gtag/js?id=" + encodeURIComponent(GA_ID);
    document.head.appendChild(s);
  };
  var stopAnalytics = function () {
    if (GA_ID) window["ga-disable-" + GA_ID] = true;
    // Forget the _ga cookies a previous "yes" left behind.
    var host = location.hostname;
    var domains = ["", host, "." + host, "." + host.split(".").slice(-2).join(".")];
    document.cookie.split(";").forEach(function (c) {
      var name = c.split("=")[0].trim();
      if (!/^_ga/.test(name)) return;
      domains.forEach(function (d) {
        document.cookie = name + "=; Max-Age=0; path=/" + (d ? "; domain=" + d : "");
      });
    });
  };
  var banner = null;
  var closeBanner = function () {
    if (!banner) return;
    banner.remove();
    banner = null;
    document.body.classList.remove("has-consent");
  };
  var answer = function (yes) {
    saveConsent(yes ? "granted" : "denied");
    if (yes) loadAnalytics(); else stopAnalytics();
    closeBanner();
  };
  var showBanner = function (focus) {
    if (banner || !GA_ID) return;
    banner = document.createElement("section");
    banner.className = "consent";
    banner.setAttribute("aria-labelledby", "consent-title");
    banner.innerHTML =
      '<h2 id="consent-title" class="consent-title">Can we count your visit?</h2>' +
      "<p>With your permission, Google Analytics shows us which pages help people learn. No ads, nothing sold, " +
      "and nothing loads unless you say yes. Change your mind any time in <em>Privacy choices</em>, at the bottom of every page.</p>" +
      '<div class="consent-actions">' +
      '<button type="button" class="btn btn-gold" data-consent="yes">Yes, count my visit</button>' +
      '<button type="button" class="btn btn-line" data-consent="no">No thanks</button>' +
      "</div>";
    banner.addEventListener("click", function (e) {
      var b = e.target.closest && e.target.closest("[data-consent]");
      if (b) answer(b.getAttribute("data-consent") === "yes");
    });
    document.body.appendChild(banner);
    document.body.classList.add("has-consent");
    if (focus) banner.querySelector("button").focus();
  };
  if (GA_ID) {
    var consent = readConsent();
    if (consent === "granted") loadAnalytics();
    else if (consent !== "denied" && !privacySignal()) showBanner(false);
    document.querySelectorAll("[data-privacy-choices]").forEach(function (b) {
      b.hidden = false;
      b.addEventListener("click", function () { showBanner(true); });
    });
  }

  // ---------- Copy code ----------
  document.querySelectorAll(".copy-btn").forEach(function (btn) {
    btn.addEventListener("click", function () {
      var pre = document.getElementById(btn.getAttribute("data-target"));
      if (!pre || !navigator.clipboard) return;
      navigator.clipboard.writeText(pre.textContent).then(function () {
        btn.textContent = "Copied";
        setTimeout(function () { btn.textContent = "Copy code"; }, 1800);
      });
    });
  });

  // ---------- Run in Dana: danaide://run?code=<base64url> ----------
  // Same base64url the IDE's Program.cs decodes. There's no reliable way to
  // know whether the protocol is registered: if the page is still in front a
  // moment later, assume no IDE answered and say so.
  function toBase64Url(str) {
    var bytes = new TextEncoder().encode(str);
    var binary = "";
    bytes.forEach(function (b) { binary += String.fromCharCode(b); });
    return btoa(binary).replace(/\+/g, "-").replace(/\//g, "_").replace(/=+$/, "");
  }
  function setLabel(btn, text) { var l = btn.querySelector(".dana-test-label"); if (l) l.textContent = text; }
  document.querySelectorAll(".dana-test-btn").forEach(function (btn) {
    btn.addEventListener("click", function () {
      var pre = document.getElementById(btn.getAttribute("data-target"));
      if (!pre) return;
      var stillHere = true;
      var away = function () { stillHere = false; };
      window.addEventListener("blur", away, { once: true });
      document.addEventListener("visibilitychange", away, { once: true });
      btn.classList.remove("is-error");
      setLabel(btn, "Opening…");
      window.location.href = "danaide://run?code=" + toBase64Url(pre.textContent);
      setTimeout(function () {
        window.removeEventListener("blur", away);
        document.removeEventListener("visibilitychange", away);
        if (stillHere) {
          btn.classList.add("is-error");
          setLabel(btn, "IDE not found — install it first");
          setTimeout(function () { btn.classList.remove("is-error"); setLabel(btn, "Run in Dana"); }, 3200);
        } else {
          setLabel(btn, "Run in Dana");
        }
      }, 1500);
    });
  });

  // ---------- A–Z filter ----------
  var azInput = document.getElementById("az-q");
  if (azInput) {
    var items = Array.prototype.slice.call(document.querySelectorAll(".az-letter li"));
    var count = document.getElementById("az-count");
    var apply = function () {
      var q = norm(azInput.value.trim());
      var shown = 0;
      items.forEach(function (li) {
        var hit = !q || li.getAttribute("data-key").indexOf(q) >= 0;
        li.hidden = !hit;
        if (hit) shown++;
      });
      document.querySelectorAll(".az-letter").forEach(function (sec) {
        sec.hidden = !sec.querySelector("li:not([hidden])");
      });
      count.textContent = q ? shown + " of " + items.length + " names" : items.length + " names";
    };
    azInput.addEventListener("input", apply);
    var fromSearch = new URLSearchParams(location.search).get("q");
    if (fromSearch) { azInput.value = fromSearch; apply(); }
  }

  // ---------- Downloads: link each platform to the real file of the latest release ----------
  var platformLinks = document.querySelectorAll("[data-platform], .download-primary");
  if (platformLinks.length) {
    var REPO = "BuddhaCodes/DanaProcessing";
    var PLATFORMS = [
      { key: "win", label: "Windows", match: function (n) { return /\.exe$/i.test(n); } },
      { key: "osx-arm64", label: "macOS", match: function (n) { return /\.pkg$/i.test(n) && /arm64/i.test(n); } },
      { key: "osx-x64", label: "macOS (Intel)", match: function (n) { return /\.pkg$/i.test(n) && !/arm64/i.test(n) && /(x64|x86_64|intel)/i.test(n); } },
      { key: "linux", label: "Linux", match: function (n) { return /\.appimage$/i.test(n); } }
    ];
    var detect = function () {
      var ua = navigator.userAgent || "";
      if (/Win/i.test(ua)) return "win";
      if (/Mac/i.test(ua)) return "osx-arm64";
      if (/Linux/i.test(ua) && !/Android/i.test(ua)) return "linux";
      return null;
    };
    var byKey = function (k) { return PLATFORMS.filter(function (p) { return p.key === k; })[0]; };
    var find = function (assets, p) { return assets.filter(function (a) { return p.match(a.name); })[0]; };
    fetch("https://api.github.com/repos/" + REPO + "/releases/latest")
      .then(function (r) { return r.ok ? r.json() : Promise.reject(r.status); })
      .then(function (release) {
        var assets = release.assets || [];
        document.querySelectorAll("[data-platform]").forEach(function (a) {
          var asset = find(assets, byKey(a.getAttribute("data-platform")));
          if (asset) a.href = asset.browser_download_url;
        });
        var p = byKey(detect());
        var asset = p && find(assets, p);
        // "v1.0.0" -> "1.0": the button says which version you're getting.
        var version = String(release.tag_name || "").replace(/^v/, "").replace(/\.0$/, "");
        document.querySelectorAll(".download-primary").forEach(function (a) {
          if (asset) a.href = asset.browser_download_url;
          var label = a.querySelector(".download-primary-label");
          if (label && p) label.textContent = "Download " + (version ? version + " " : "") + "for " + p.label;
        });
      })
      .catch(function () { /* keep the links to the releases page */ });
  }
})();
