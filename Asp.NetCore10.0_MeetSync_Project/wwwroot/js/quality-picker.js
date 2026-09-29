"use strict";

function initializeQualityPicker() {
    const select = document.getElementById("share-quality");
    const trigger = document.getElementById("quality-trigger");
    const menu = document.getElementById("quality-menu");
    const choices = [...menu.querySelectorAll("[data-quality]")];
    // Keep the floating panel outside the toolbar's transformed stacking context.
    document.body.appendChild(menu);
    const labels = { "480": "480p", "720": "720p", "1080": "1080p", "1440": "2K", "2160": "4K" };

    function sync() {
        trigger.disabled = select.disabled;
        document.getElementById("quality-value").textContent = labels[select.value];
        trigger.setAttribute("aria-label", "Ekran kalitesi: " + labels[select.value] + ". Değiştir");
        choices.forEach(choice => {
            const selected = choice.dataset.quality === select.value;
            choice.setAttribute("aria-checked", String(selected));
            choice.tabIndex = selected ? 0 : -1;
            choice.disabled = select.disabled;
        });
        if (select.disabled) close();
    }
    function close(returnFocus = false) {
        menu.hidden = true;
        trigger.setAttribute("aria-expanded", "false");
        if (returnFocus) trigger.focus();
    }
    function open() {
        if (select.disabled) return;
        sync();
        menu.hidden = false;
        const rect = trigger.getBoundingClientRect();
        menu.style.left = Math.max(12, Math.min(rect.left, window.innerWidth - menu.offsetWidth - 12)) + "px";
        menu.style.top = Math.max(12, rect.top - menu.offsetHeight - 14) + "px";
        trigger.setAttribute("aria-expanded", "true");
        choices.find(choice => choice.dataset.quality === select.value)?.focus();
    }
    async function choose(choice) {
        if (select.disabled) return;
        select.value = choice.dataset.quality;
        close(true);
        // Reuse the existing asynchronous quality change, including rollback on failure.
        try { await select.onchange.call(select); }
        finally { sync(); }
    }
    trigger.addEventListener("click", () => menu.hidden ? open() : close());
    trigger.addEventListener("keydown", event => {
        if (event.key === "ArrowUp" || event.key === "ArrowDown") {
            event.preventDefault(); open();
        }
    });
    choices.forEach(choice => choice.addEventListener("click", () => { void choose(choice); }));
    menu.addEventListener("keydown", event => {
        if (event.key === "Escape") { event.preventDefault(); close(true); return; }
        if (event.key === "Tab") { close(true); return; }
        const direction = { ArrowDown: 1, ArrowRight: 1, ArrowUp: -1, ArrowLeft: -1 }[event.key];
        if (direction || event.key === "Home" || event.key === "End") {
            event.preventDefault();
            const index = choices.indexOf(document.activeElement);
            const next = event.key === "Home" ? 0 : event.key === "End" ? choices.length - 1 :
                (index + direction + choices.length) % choices.length;
            void choose(choices[next]);
        }
    });
    document.addEventListener("pointerdown", event => {
        if (!menu.contains(event.target) && !trigger.contains(event.target)) close();
    });
    window.addEventListener("resize", () => close());
    window.addEventListener("scroll", () => close(), true);
    new MutationObserver(sync).observe(select, { attributes: true, attributeFilter: ["disabled"] });
    sync();
}
