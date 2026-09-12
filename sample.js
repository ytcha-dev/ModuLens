// ==UserScript==
// @name         IG Overlay Video Transformer 1.2.1
// @namespace    ig-transformer
// @version      1.2.1
// @description  Instagram video controls + overlay fullscreen transformer.
// @match        https://www.instagram.com/*
// @grant        none
// @run-at       document-idle
// ==/UserScript==

(function () {
    'use strict';

    /**
     * ============================================================
     * Config
     * ============================================================
     */

    const CONFIG = {
        ZOOM_STEP: 0.05,
        PAN_STEP: 40,

        MIN_SCALE: 0.1,
        MAX_SCALE: 8,

        SEEK_STEP_SECONDS: 5,

        VOLUME_STEP: 0.05,

        DEFAULT_PLAYBACK_RATE: 1,

        PLAYBACK_RATES: [
            0.25,
            0.5,
            0.75,
            1,
            1.25,
            1.5,
            1.75,
            2,
            2.5,
            3
        ],

        NORMAL_CONTROLS_ENABLED: true,

        NORMAL_BAR_POSITION: 'bottom-center',

        NORMAL_BAR_BOTTOM_OFFSET: 8,

        OVERLAY_BAR_BOTTOM_OFFSET: 8,

        HIDE_NATIVE_CONTROLS: false,

        SCAN_INTERVAL_MS: 800,

        REPOSITION_THROTTLE_MS: 120,

        DEBUG: true,

        SPEED_STEP: 0.25,

        MIN_PLAYBACK_RATE: 0.25,
        MAX_PLAYBACK_RATE: 3,

        DRAG_HOLD_MS: 100,

        CLICK_MOVE_TOLERANCE: 6,
    };

    /**
     * ============================================================
     * State
     * ============================================================
     */

    let scale = 1;
    let tx = 0;
    let ty = 0;

    let activeVideo = null;
    let normalBarVideo = null;

    let overlay = null;
    let stage = null;

    let normalBar = null;
    let overlayBar = null;
    let currentBar = null;

    let placeholder = null;
    let originalParent = null;
    let originalNextSibling = null;

    let isOverlayMode = false;

    let isDragging = false;
    let isPendingClickOrDrag = false;

    let dragStartX = 0;
    let dragStartY = 0;

    let dragStartTx = 0;
    let dragStartTy = 0;

    let pointerDownX = 0;
    let pointerDownY = 0;

    let dragHoldTimer = null;

    let repositionTimer = null;
    let scanInProgress = false;

    const originalInlineStyles = new WeakMap();
    const videoEventBound = new WeakSet();

    /**
     * ============================================================
     * Debug
     * ============================================================
     */

    function debug(...args) {
        if (!CONFIG.DEBUG) return;

        console.log('[IG Transformer]', ...args);
    }

    /**
     * ============================================================
     * Helpers
     * ============================================================
     */

    function clamp(value, min, max) {
        return Math.min(max, Math.max(min, value));
    }

    function roundToTwo(value) {
        return Math.round(value * 100) / 100;
    }

    function formatTime(seconds) {
        if (!Number.isFinite(seconds)) {
            return '00:00';
        }

        const m = Math.floor(seconds / 60);
        const s = Math.floor(seconds % 60);

        return `${String(m).padStart(2, '0')}:${String(s).padStart(2, '0')}`;
    }

    /**
     * ============================================================
     * Style helpers
     * ============================================================
     */

    function rememberStyle(el, prop) {
        if (!el) return;

        if (!originalInlineStyles.has(el)) {
            originalInlineStyles.set(el, new Map());
        }

        const map = originalInlineStyles.get(el);

        if (!map.has(prop)) {
            map.set(prop, {
                value: el.style.getPropertyValue(prop),
                priority: el.style.getPropertyPriority(prop)
            });
        }
    }

    function setImportant(el, prop, value) {
        if (!el) return;

        rememberStyle(el, prop);

        el.style.setProperty(prop, value, 'important');
    }

    function restoreStyle(el, prop) {
        const map = originalInlineStyles.get(el);

        if (!map || !map.has(prop)) {
            return;
        }

        const old = map.get(prop);

        if (old.value) {
            el.style.setProperty(
                prop,
                old.value,
                old.priority
            );
        } else {
            el.style.removeProperty(prop);
        }
    }

    function restoreMany(el, props) {
        for (const prop of props) {
            restoreStyle(el, prop);
        }
    }

    /**
     * ============================================================
     * Video detection
     * ============================================================
     */

    function isVisibleVideo(video) {
        if (!(video instanceof HTMLVideoElement)) {
            return false;
        }

        if (!document.contains(video)) {
            return false;
        }

        const r = video.getBoundingClientRect();

        if (
            r.width <= 80 ||
            r.height <= 80
        ) {
            return false;
        }

        if (
            r.bottom <= 0 ||
            r.right <= 0
        ) {
            return false;
        }

        if (
            r.top >= window.innerHeight ||
            r.left >= window.innerWidth
        ) {
            return false;
        }

        const cs = getComputedStyle(video);

        return (
            cs.display !== 'none' &&
            cs.visibility !== 'hidden' &&
            Number(cs.opacity) !== 0
        );
    }

    function getCenterBonus(rect) {
        const cx = rect.left + rect.width / 2;
        const cy = rect.top + rect.height / 2;

        const dx = Math.abs(
            cx - window.innerWidth / 2
        );

        const dy = Math.abs(
            cy - window.innerHeight / 2
        );

        return Math.max(
            0,
            1_000_000 - dx * 1000 - dy * 1000
        );
    }

    function getBestVideo() {
        const videos = document.querySelectorAll('video');

        let best = null;
        let bestScore = -Infinity;

        for (const video of videos) {
            if (!isVisibleVideo(video)) {
                continue;
            }

            const r = video.getBoundingClientRect();

            const score =
                r.width * r.height +
                getCenterBonus(r) +
                (video.paused ? 0 : 10_000_000);

            if (score > bestScore) {
                best = video;
                bestScore = score;
            }
        }

        return best;
    }

    function getVideoFromPoint(x, y) {
        let el = document.elementFromPoint(x, y);

        for (let i = 0; i < 10 && el; i++) {
            if (el instanceof HTMLVideoElement) {
                return el;
            }

            const childVideo =
                el.querySelector?.('video');

            if (
                childVideo instanceof HTMLVideoElement
            ) {
                return childVideo;
            }

            el = el.parentElement;
        }

        return null;
    }

    function getFallbackVideo() {
        const videos = [
            ...document.querySelectorAll('video')
        ];

        if (videos.length === 0) {
            return null;
        }

        const playing = videos.find(video =>
            !video.paused &&
            document.contains(video)
        );

        if (playing) {
            return playing;
        }

        const centerVideo = getVideoFromPoint(
            window.innerWidth / 2,
            window.innerHeight / 2
        );

        if (centerVideo) {
            return centerVideo;
        }

        return videos
            .filter(video =>
                document.contains(video)
            )
            .sort((a, b) => {
                const ar =
                    a.getBoundingClientRect();

                const br =
                    b.getBoundingClientRect();

                return (
                    br.width * br.height -
                    ar.width * ar.height
                );
            })[0] ?? null;
    }

    function getCurrentVideo() {
        return (
            getBestVideo() ||
            getFallbackVideo() ||
            activeVideo
        );
    }

    /**
     * ============================================================
     * Normal controls
     * ============================================================
     */

    function installNormalControls(video) {
        if (!CONFIG.NORMAL_CONTROLS_ENABLED) {
            return;
        }

        if (!video || isOverlayMode) {
            return;
        }

        if (
            normalBar &&
            normalBarVideo !== video
        ) {
            removeNormalControls();
        }

        activeVideo = video;
        normalBarVideo = video;

        if (CONFIG.HIDE_NATIVE_CONTROLS) {
            if (
                !video.dataset
                    .igTransformerNormalHadControls
            ) {
                video.dataset
                    .igTransformerNormalHadControls =
                    video.hasAttribute('controls')
                        ? '1'
                        : '0';
            }

            video.removeAttribute('controls');
        }

        bindVideoEvents(video);

        if (
            !normalBar ||
            !document.contains(normalBar)
        ) {
            normalBar = createControlBar({
                mode: 'normal',
                video
            });

            document.body.appendChild(normalBar);
        }

        currentBar = normalBar;

        positionNormalBar(video);

        updateControls();
    }

    function positionNormalBar(video) {
        if (
            !normalBar ||
            !video ||
            isOverlayMode
        ) {
            return;
        }

        if (!document.contains(video)) {
            return;
        }

        const r = video.getBoundingClientRect();

        if (
            r.width <= 80 ||
            r.height <= 80 ||
            r.bottom <= 0 ||
            r.right <= 0 ||
            r.top >= window.innerHeight ||
            r.left >= window.innerWidth
        ) {
            normalBar.style.display = 'none';

            return;
        }

        const bottom =
            window.innerHeight -
            r.bottom +
            CONFIG.NORMAL_BAR_BOTTOM_OFFSET;

        normalBar.style.position = 'fixed';

        normalBar.style.bottom =
            `${Math.max(8, bottom)}px`;

        if (
            CONFIG.NORMAL_BAR_POSITION ===
            'bottom-left'
        ) {
            normalBar.style.left =
                `${Math.round(r.left + 12)}px`;

            normalBar.style.right = 'auto';

            normalBar.style.transform = 'none';
        }

        else if (
            CONFIG.NORMAL_BAR_POSITION ===
            'bottom-right'
        ) {
            normalBar.style.left = 'auto';

            normalBar.style.right =
                `${Math.round(
                    window.innerWidth -
                    r.right +
                    12
                )}px`;

            normalBar.style.transform = 'none';
        }

        else {
            normalBar.style.left =
                `${Math.round(
                    r.left + r.width / 2
                )}px`;

            normalBar.style.right = 'auto';

            normalBar.style.transform =
                'translateX(-50%)';
        }

        normalBar.style.display = 'flex';
    }

    function removeNormalControls() {
        if (normalBar) {
            normalBar.remove();
            normalBar = null;
        }

        normalBarVideo = null;

        currentBar = null;
    }

    /**
     * ============================================================
     * Overlay lifecycle
     * ============================================================
     */

    async function enterOverlayFullscreen(video) {
        if (!video || isOverlayMode) {
            return;
        }

        activeVideo = video;

        isOverlayMode = true;

        removeNormalControls();

        scale = 1;
        tx = 0;
        ty = 0;

        rememberVideoOriginalLocation(video);

        createOverlay();

        moveVideoIntoOverlay(video);

        prepareVideoForOverlay(video);

        installOverlayControls(video);

        document.body.appendChild(overlay);

        try {
            await overlay.requestFullscreen();
        }

        catch (err) {
            console.error(
                '[IG Transformer] requestFullscreen failed:',
                err
            );

            cleanupOverlay();

            return;
        }

        applyTransform();

        debug(
            'overlay fullscreen entered',
            {
                fullscreenElement:
                    document.fullscreenElement,
                video
            }
        );
    }

    function rememberVideoOriginalLocation(video) {
        originalParent = video.parentElement;

        originalNextSibling = video.nextSibling;

        placeholder =
            document.createComment(
                'ig-transformer-video-placeholder'
            );

        if (originalParent) {
            originalParent.insertBefore(
                placeholder,
                video
            );
        }
    }

    function createOverlay() {
        overlay = document.createElement('div');

        overlay.id =
            'ig-transformer-overlay';

        Object.assign(
            overlay.style,
            {
                position: 'fixed',
                inset: '0',
                zIndex: '2147483647',

                background: 'black',

                display: 'flex',

                alignItems: 'center',
                justifyContent: 'center',

                overflow: 'hidden'
            }
        );

        stage = document.createElement('div');

        stage.id =
            'ig-transformer-stage';

        Object.assign(
            stage.style,
            {
                position: 'relative',

                width: '100vw',
                height: '100vh',

                display: 'flex',

                alignItems: 'center',
                justifyContent: 'center',

                overflow: 'hidden',

                background: 'black',

                cursor: 'grab',

                userSelect: 'none'
            }
        );

        overlay.appendChild(stage);

        installMouseDrag(stage);

        installWheelZoom(stage);
    }

    function moveVideoIntoOverlay(video) {
        stage.appendChild(video);
    }

    function prepareVideoForOverlay(video) {
        video.dataset
            .igTransformerOverlayHadControls =
            video.hasAttribute('controls')
                ? '1'
                : '0';

        video.removeAttribute('controls');

        setImportant(
            video,
            'max-width',
            '100vw'
        );

        setImportant(
            video,
            'max-height',
            '100vh'
        );

        setImportant(
            video,
            'width',
            'auto'
        );

        setImportant(
            video,
            'height',
            'auto'
        );

        setImportant(
            video,
            'object-fit',
            'contain'
        );

        setImportant(
            video,
            'transition',
            'none'
        );

        setImportant(
            video,
            'will-change',
            'transform'
        );

        setImportant(
            video,
            'transform-origin',
            'center center'
        );

        setImportant(
            video,
            'background',
            'black'
        );
    }

    function installOverlayControls(video) {
        overlayBar = createControlBar({
            mode: 'overlay',
            video
        });

        Object.assign(
            overlayBar.style,
            {
                position: 'fixed',

                left: '50%',
                right: 'auto',

                bottom:
                    `${CONFIG.OVERLAY_BAR_BOTTOM_OFFSET}px`,

                transform:
                    'translateX(-50%)'
            }
        );

        overlay.appendChild(overlayBar);

        currentBar = overlayBar;

        bindVideoEvents(video);

        updateControls();
    }

    function cleanupOverlay() {
        const video = activeVideo;

        if (overlayBar) {
            overlayBar.remove();

            overlayBar = null;
        }

        currentBar = null;

        if (video) {
            video.style.removeProperty(
                'transform'
            );

            restoreMany(
                video,
                [
                    'max-width',
                    'max-height',
                    'width',
                    'height',
                    'object-fit',
                    'transition',
                    'will-change',
                    'transform-origin',
                    'background'
                ]
            );

            restoreVideoControlsAfterOverlay(
                video
            );

            restoreVideoOriginalLocation(
                video
            );
        }

        if (overlay) {
            overlay.remove();
        }

        overlay = null;
        stage = null;

        placeholder = null;

        originalParent = null;
        originalNextSibling = null;

        scale = 1;
        tx = 0;
        ty = 0;

        isOverlayMode = false;

        isDragging = false;
        isPendingClickOrDrag = false;

        clearDragHoldTimer();

        activeVideo = null;
        normalBarVideo = null;

        refreshNormalControls();

        debug('overlay cleaned');
    }

    function restoreVideoControlsAfterOverlay(video) {
        if (CONFIG.HIDE_NATIVE_CONTROLS) {
            video.removeAttribute('controls');
        }

        else if (
            video.dataset
                .igTransformerOverlayHadControls ===
            '1'
        ) {
            video.setAttribute(
                'controls',
                ''
            );
        }

        else {
            video.removeAttribute('controls');
        }

        delete video.dataset
            .igTransformerOverlayHadControls;
    }

    function restoreVideoOriginalLocation(video) {
        if (
            placeholder &&
            placeholder.parentNode
        ) {
            placeholder.parentNode
                .insertBefore(
                    video,
                    placeholder
                );

            placeholder.remove();

            return;
        }

        if (originalParent) {
            originalParent.insertBefore(
                video,
                originalNextSibling
            );
        }
    }

    /**
     * ============================================================
     * Control bar
     * ============================================================
     */

    function createControlBar({
        mode,
        video
    }) {
        const bar =
            document.createElement('div');

        bar.className =
            `ig-transformer-controls ig-transformer-controls-${mode}`;

        const transformButtons =
            mode === 'overlay'
                ? `
                    <button
                        type="button"
                        data-act="zoom-out"
                    >
                        －
                    </button>

                    <button
                        type="button"
                        data-act="zoom-in"
                    >
                        ＋
                    </button>

                    <button
                        type="button"
                        data-act="reset"
                    >
                        Reset
                    </button>
                `
                : '';

        bar.innerHTML = `
            <button
                type="button"
                data-act="play"
            >
                ▶/⏸
            </button>

            <button
                type="button"
                data-act="back"
            >
                -${CONFIG.SEEK_STEP_SECONDS}s
            </button>

            <input
                type="range"
                data-role="seek"
                min="0"
                max="1000"
                value="0"
            >

            <button
                type="button"
                data-act="forward"
            >
                +${CONFIG.SEEK_STEP_SECONDS}s
            </button>

            <select
                data-role="speed"
                title="Playback speed"
            ></select>

            ${transformButtons}

            ${
                mode === 'normal'

                    ? `
                        <button
                            type="button"
                            data-act="fullscreen"
                        >
                            ⛶
                        </button>
                    `

                    : `
                        <button
                            type="button"
                            data-act="exit"
                        >
                            Exit
                        </button>
                    `
            }

            <span data-role="time">
                00:00 / 00:00
            </span>
        `;

        applyControlBarStyle(bar);

        const speedSelect =
            bar.querySelector(
                '[data-role="speed"]'
            );

        for (
            const rate of
            CONFIG.PLAYBACK_RATES
        ) {
            const option =
                document.createElement(
                    'option'
                );

            option.value =
                String(rate);

            option.textContent =
                `${rate}x`;

            if (
                rate ===
                CONFIG.DEFAULT_PLAYBACK_RATE
            ) {
                option.selected = true;
            }

            speedSelect.appendChild(
                option
            );
        }

        if (
            !video.playbackRate ||
            video.playbackRate === 1
        ) {
            video.playbackRate =
                CONFIG.DEFAULT_PLAYBACK_RATE;
        }

        speedSelect.value =
            String(
                video.playbackRate ||
                CONFIG.DEFAULT_PLAYBACK_RATE
            );

        /**
         * 非常重要：
         *
         * IG / React 外層有很多 pointer / mouse handler。
         *
         * 所以控制列的事件要在 capture 階段先攔。
         *
         * 不然 Fullscreen 裡面的
         * -5s / +5s / play / reset / exit
         * 有可能被 IG 吃掉。
         */

        const stopControlPointerEvent =
            e => {
                e.stopPropagation();
            };

        bar.addEventListener(
            'pointerdown',
            stopControlPointerEvent,
            true
        );

        bar.addEventListener(
            'pointerup',
            stopControlPointerEvent,
            true
        );

        bar.addEventListener(
            'mousedown',
            stopControlPointerEvent,
            true
        );

        bar.addEventListener(
            'mouseup',
            stopControlPointerEvent,
            true
        );

        bar.addEventListener(
            'contextmenu',
            e => {
                e.preventDefault();

                e.stopPropagation();
            },
            true
        );

        /**
         * 控制列 click
         */

        bar.addEventListener(
            'click',
            async e => {
                const button =
                    e.target.closest(
                        'button'
                    );

                if (!button) {
                    return;
                }

                e.preventDefault();

                e.stopPropagation();

                await handleControlAction(
                    button.dataset.act,
                    video
                );

                updateControls();
            },
            true
        );

        /**
         * Seek
         */

        const seek =
            bar.querySelector(
                '[data-role="seek"]'
            );

        seek.addEventListener(
            'input',
            e => {
                e.preventDefault();

                e.stopPropagation();

                if (
                    !Number.isFinite(
                        video.duration
                    ) ||
                    video.duration <= 0
                ) {
                    return;
                }

                const ratio =
                    Number(seek.value) /
                    1000;

                video.currentTime =
                    video.duration *
                    ratio;
            }
        );

        /**
         * Speed
         */

        speedSelect.addEventListener(
            'change',
            e => {
                e.preventDefault();

                e.stopPropagation();

                const rate =
                    Number(
                        speedSelect.value
                    );

                if (
                    Number.isFinite(rate) &&
                    rate > 0
                ) {
                    video.playbackRate =
                        rate;

                    debug(
                        'playback rate changed',
                        rate
                    );
                }

                updateControls();
            }
        );

        return bar;
    }

    function applyControlBarStyle(bar) {
        Object.assign(
            bar.style,
            {
                zIndex: '2147483647',

                display: 'flex',

                gap: '8px',

                alignItems: 'center',

                padding: '8px 10px',

                background:
                    'rgba(0,0,0,.72)',

                color: '#fff',

                borderRadius: '10px',

                fontSize: '13px',

                fontFamily:
                    'system-ui, sans-serif',

                pointerEvents: 'auto',

                boxShadow:
                    '0 8px 24px rgba(0,0,0,.35)',

                backdropFilter:
                    'blur(4px)'
            }
        );

        bar
            .querySelectorAll('button')
            .forEach(button => {
                Object.assign(
                    button.style,
                    {
                        color: '#fff',

                        background:
                            'rgba(255,255,255,.16)',

                        border:
                            '1px solid rgba(255,255,255,.25)',

                        borderRadius:
                            '6px',

                        padding:
                            '5px 8px',

                        cursor:
                            'pointer',

                        fontSize:
                            '13px'
                    }
                );
            });

        const seek =
            bar.querySelector(
                '[data-role="seek"]'
            );

        if (seek) {
            Object.assign(
                seek.style,
                {
                    width: '180px',

                    cursor:
                        'pointer'
                }
            );
        }

        const speed =
            bar.querySelector(
                '[data-role="speed"]'
            );

        if (speed) {
            Object.assign(
                speed.style,
                {
                    color: '#fff',

                    background:
                        'rgba(0,0,0,.6)',

                    border:
                        '1px solid rgba(255,255,255,.25)',

                    borderRadius:
                        '6px',

                    padding:
                        '4px 6px',

                    cursor:
                        'pointer',

                    fontSize:
                        '13px'
                }
            );
        }
    }

    /**
     * ============================================================
     * Control actions
     * ============================================================
     */

    async function handleControlAction(
        action,
        video
    ) {
        switch (action) {

            case 'play': {
                if (video.paused) {
                    try {
                        await video.play();
                    } catch (err) {
                        console.warn(
                            '[IG Transformer] play failed',
                            err
                        );
                    }
                }

                else {
                    video.pause();
                }

                break;
            }

            /**
             * 防止 currentTime / duration 還沒初始化
             * 導致 Fullscreen 裡面快退無效。
             */

            case 'back': {
                const current =
                    Number.isFinite(
                        video.currentTime
                    )
                        ? video.currentTime
                        : 0;

                const next =
                    Math.max(
                        0,
                        current -
                        CONFIG.SEEK_STEP_SECONDS
                    );

                try {
                    video.currentTime = next;
                } catch (err) {
                    console.warn(
                        '[IG Transformer] seek back failed',
                        err
                    );
                }

                debug(
                    'seek back',
                    {
                        current,
                        next
                    }
                );

                break;
            }

            /**
             * forward 也做 duration 防呆。
             */

            case 'forward': {
                const current =
                    Number.isFinite(
                        video.currentTime
                    )
                        ? video.currentTime
                        : 0;

                const hasDuration =
                    Number.isFinite(
                        video.duration
                    ) &&
                    video.duration > 0;

                const next =
                    hasDuration

                        ? Math.min(
                            video.duration,
                            current +
                            CONFIG.SEEK_STEP_SECONDS
                        )

                        : current +
                          CONFIG.SEEK_STEP_SECONDS;

                try {
                    video.currentTime = next;
                } catch (err) {
                    console.warn(
                        '[IG Transformer] seek forward failed',
                        err
                    );
                }

                debug(
                    'seek forward',
                    {
                        current,
                        next,
                        duration:
                            video.duration
                    }
                );

                break;
            }

            case 'zoom-out': {
                if (isOverlayMode) {
                    zoom(
                        -CONFIG.ZOOM_STEP
                    );
                }

                break;
            }

            case 'zoom-in': {
                if (isOverlayMode) {
                    zoom(
                        CONFIG.ZOOM_STEP
                    );
                }

                break;
            }

            case 'reset': {
                if (isOverlayMode) {
                    resetTransform();
                }

                break;
            }

            case 'fullscreen': {
                await enterOverlayFullscreen(
                    video
                );

                break;
            }

            case 'exit': {
                await exitFullscreenSafely();

                break;
            }
        }
    }

    /**
     * ============================================================
     * Video events
     * ============================================================
     */

    function bindVideoEvents(video) {
        if (
            !video ||
            videoEventBound.has(video)
        ) {
            return;
        }

        video.addEventListener(
            'timeupdate',
            updateControls
        );

        video.addEventListener(
            'durationchange',
            updateControls
        );

        video.addEventListener(
            'loadedmetadata',
            updateControls
        );

        video.addEventListener(
            'play',
            updateControls
        );

        video.addEventListener(
            'pause',
            updateControls
        );

        video.addEventListener(
            'volumechange',
            updateControls
        );

        video.addEventListener(
            'ratechange',
            updateControls
        );

        videoEventBound.add(video);
    }

    function updateControls() {
        const video = activeVideo;

        const bar =
            currentBar ||
            normalBar ||
            overlayBar;

        if (!video || !bar) {
            return;
        }

        if (!document.contains(bar)) {
            return;
        }

        const timeEl =
            bar.querySelector(
                '[data-role="time"]'
            );

        const seek =
            bar.querySelector(
                '[data-role="seek"]'
            );

        const speed =
            bar.querySelector(
                '[data-role="speed"]'
            );

        if (timeEl) {
            timeEl.textContent =
                `${formatTime(
                    video.currentTime
                )} / ${formatTime(
                    video.duration
                )}`;
        }

        if (
            seek &&
            Number.isFinite(
                video.duration
            ) &&
            video.duration > 0
        ) {
            seek.value =
                String(
                    Math.round(
                        (
                            video.currentTime /
                            video.duration
                        ) *
                        1000
                    )
                );
        }

        if (
            speed &&
            String(
                video.playbackRate
            ) !== speed.value
        ) {
            speed.value =
                String(
                    video.playbackRate
                );
        }
    }

    /**
     * ============================================================
     * Transform
     * ============================================================
     */

    function applyTransform() {
        const video = activeVideo;

        if (
            !video ||
            !isOverlayMode
        ) {
            return;
        }

        video.style.setProperty(
            'transform',

            `translate(${tx}px, ${ty}px) scale(${scale})`,

            'important'
        );

        video.style.setProperty(
            'transform-origin',
            'center center',
            'important'
        );

        updateControls();

        debug(
            'transform',
            {
                scale:
                    scale.toFixed(2),

                tx,
                ty
            }
        );
    }

    function resetTransform() {
        scale = 1;
        tx = 0;
        ty = 0;

        applyTransform();
    }

    function zoom(delta) {
        scale = clamp(
            scale + delta,
            CONFIG.MIN_SCALE,
            CONFIG.MAX_SCALE
        );

        applyTransform();
    }

    function move(dx, dy) {
        tx += dx;
        ty += dy;

        applyTransform();
    }

    /**
     * ============================================================
     * Playback rate
     * ============================================================
     */

    function changePlaybackRate(delta) {
        const video =
            activeVideo ||
            getCurrentVideo();

        if (!video) {
            return;
        }

        const nextRate =
            clamp(
                roundToTwo(
                    video.playbackRate +
                    delta
                ),

                CONFIG.MIN_PLAYBACK_RATE,

                CONFIG.MAX_PLAYBACK_RATE
            );

        video.playbackRate =
            nextRate;

        updateControls();

        debug(
            'playback rate changed by mouse',
            {
                rate: nextRate,
                delta
            }
        );
    }

    /**
     * ============================================================
     * Mouse drag in overlay
     * ============================================================
     */

    function installMouseDrag(target) {

        target.addEventListener(
            'mousedown',
            e => {
                if (!isOverlayMode) {
                    return;
                }

                /**
                 * 控制列不進行拖曳。
                 */

                if (
                    e.target.closest?.(
                        '.ig-transformer-controls'
                    )
                ) {
                    return;
                }

                /**
                 * Mouse:
                 *
                 * 0 左鍵
                 * 1 中鍵
                 * 2 右鍵
                 * 3 側鍵 Back
                 * 4 側鍵 Forward
                 */

                if (e.button === 3) {
                    changePlaybackRate(
                        -CONFIG.SPEED_STEP
                    );

                    e.preventDefault();

                    e.stopImmediatePropagation();

                    return;
                }

                if (e.button === 4) {
                    changePlaybackRate(
                        CONFIG.SPEED_STEP
                    );

                    e.preventDefault();

                    e.stopImmediatePropagation();

                    return;
                }

                /**
                 * 右鍵交給 contextmenu 處理
                 */

                if (e.button === 2) {
                    return;
                }

                /**
                 * 其他非左鍵不處理
                 */

                if (e.button !== 0) {
                    return;
                }

                const video =
                    activeVideo ||
                    getCurrentVideo();

                if (!video) {
                    return;
                }

                isPendingClickOrDrag = true;

                isDragging = false;

                pointerDownX =
                    e.clientX;

                pointerDownY =
                    e.clientY;

                dragStartX =
                    e.clientX;

                dragStartY =
                    e.clientY;

                dragStartTx = tx;

                dragStartTy = ty;

                clearDragHoldTimer();

                /**
                 * 按住一小段時間後才開始拖曳。
                 */

                dragHoldTimer =
                    window.setTimeout(
                        () => {
                            if (
                                !isPendingClickOrDrag
                            ) {
                                return;
                            }

                            isDragging = true;

                            target.style.cursor =
                                'grabbing';

                            debug(
                                'drag started'
                            );
                        },

                        CONFIG.DRAG_HOLD_MS
                    );

                e.preventDefault();

                e.stopPropagation();
            }
        );

        /**
         * mousemove 放在 window capture，
         * 即使游標離開 video 還能繼續拖。
         */

        window.addEventListener(
            'mousemove',
            e => {
                if (!isOverlayMode) {
                    return;
                }

                if (
                    !isPendingClickOrDrag &&
                    !isDragging
                ) {
                    return;
                }

                if (!isDragging) {
                    e.preventDefault();

                    e.stopPropagation();

                    return;
                }

                tx =
                    dragStartTx +
                    (
                        e.clientX -
                        dragStartX
                    );

                ty =
                    dragStartTy +
                    (
                        e.clientY -
                        dragStartY
                    );

                applyTransform();

                e.preventDefault();

                e.stopPropagation();
            },
            true
        );

        /**
         * mouseup
         */

        window.addEventListener(
            'mouseup',
            async e => {
                if (!isOverlayMode) {
                    return;
                }

                /**
                 * 側鍵 mouseup 擋掉，
                 * 避免瀏覽器上一頁/下一頁。
                 */

                if (
                    e.button === 3 ||
                    e.button === 4
                ) {
                    e.preventDefault();

                    e.stopImmediatePropagation();

                    return;
                }

                if (e.button !== 0) {
                    return;
                }

                if (
                    !isPendingClickOrDrag &&
                    !isDragging
                ) {
                    return;
                }

                const wasDragging =
                    isDragging;

                const movedDistance =
                    Math.hypot(
                        e.clientX -
                        pointerDownX,

                        e.clientY -
                        pointerDownY
                    );

                clearDragHoldTimer();

                isPendingClickOrDrag =
                    false;

                isDragging = false;

                if (stage) {
                    stage.style.cursor =
                        'grab';
                }

                /**
                 * 沒拖曳就是單擊播放 / 暫停。
                 */

                if (
                    !wasDragging &&
                    movedDistance <=
                    CONFIG.CLICK_MOVE_TOLERANCE
                ) {
                    const video =
                        activeVideo ||
                        getCurrentVideo();

                    if (video) {
                        if (video.paused) {
                            try {
                                await video.play();
                            } catch (_) {}
                        }

                        else {
                            video.pause();
                        }

                        updateControls();

                        debug(
                            'click toggled play'
                        );
                    }
                }

                e.preventDefault();

                e.stopPropagation();
            },
            true
        );

        /**
         * 側鍵 auxclick。
         */

        target.addEventListener(
            'auxclick',
            e => {
                if (!isOverlayMode) {
                    return;
                }

                if (
                    e.button === 3 ||
                    e.button === 4
                ) {
                    e.preventDefault();

                    e.stopImmediatePropagation();
                }
            },
            true
        );
    }

    function clearDragHoldTimer() {
        if (!dragHoldTimer) {
            return;
        }

        window.clearTimeout(
            dragHoldTimer
        );

        dragHoldTimer = null;
    }

    /**
     * ============================================================
     * Wheel zoom
     * ============================================================
     */

    function installWheelZoom(target) {
        target.addEventListener(
            'wheel',
            e => {
                if (!isOverlayMode) {
                    return;
                }

                /**
                 * 控制列上的滾輪不要縮放影片。
                 */

                if (
                    e.target.closest?.(
                        '.ig-transformer-controls'
                    )
                ) {
                    return;
                }

                const delta =
                    e.deltaY < 0
                        ? CONFIG.ZOOM_STEP
                        : -CONFIG.ZOOM_STEP;

                zoom(delta);

                e.preventDefault();

                e.stopPropagation();
            },
            {
                passive: false
            }
        );
    }

    /**
     * ============================================================
     * RIGHT CLICK
     *
     * 這一段就是這次最重要的修正。
     *
     * Normal:
     *
     *     影片上右鍵
     *         ↓
     *     進入 overlay fullscreen
     *
     * Fullscreen:
     *
     *     非控制列區域右鍵
     *         ↓
     *     離開 fullscreen
     *
     * 使用 document capture，
     * 優先於 Instagram / React handler。
     * ============================================================
     */

    document.addEventListener(
        'contextmenu',
        async e => {

            /**
             * ----------------------------------------------------
             * 控制列
             * ----------------------------------------------------
             *
             * 控制列內右鍵不觸發 fullscreen toggle。
             */

            if (
                e.target.closest?.(
                    '.ig-transformer-controls'
                )
            ) {
                e.preventDefault();

                e.stopImmediatePropagation();

                return;
            }

            /**
             * ----------------------------------------------------
             * 已在 overlay
             * ----------------------------------------------------
             *
             * 右鍵 => 離開 fullscreen。
             */

            if (isOverlayMode) {
                e.preventDefault();

                e.stopImmediatePropagation();

                await exitFullscreenSafely();

                return;
            }

            /**
             * ----------------------------------------------------
             * 正常 IG
             * ----------------------------------------------------
             */

            const pointVideo =
                getVideoFromPoint(
                    e.clientX,
                    e.clientY
                );

            /**
             * 右鍵必須真的落在 video 附近。
             *
             * 不使用單純 activeVideo，
             * 避免你在留言區右鍵也被拉進 fullscreen。
             */

            let video =
                pointVideo;

            if (!video) {
                /**
                 * IG 有時 video 上面蓋一層 div。
                 *
                 * elementFromPoint 找不到時，
                 * 再利用目前主影片 rect 判定。
                 */

                const candidate =
                    getCurrentVideo();

                if (candidate) {
                    const rect =
                        candidate
                            .getBoundingClientRect();

                    const inside =
                        e.clientX >= rect.left &&
                        e.clientX <= rect.right &&
                        e.clientY >= rect.top &&
                        e.clientY <= rect.bottom;

                    if (inside) {
                        video = candidate;
                    }
                }
            }

            if (!video) {
                return;
            }

            if (!isVisibleVideo(video)) {
                return;
            }

            const rect =
                video.getBoundingClientRect();

            const insideVideo =
                e.clientX >= rect.left &&
                e.clientX <= rect.right &&
                e.clientY >= rect.top &&
                e.clientY <= rect.bottom;

            if (!insideVideo) {
                return;
            }

            e.preventDefault();

            e.stopImmediatePropagation();

            debug(
                'right click enter fullscreen',
                video
            );

            await enterOverlayFullscreen(
                video
            );
        },
        true
    );

    /**
     * ============================================================
     * Keyboard
     * ============================================================
     */

    window.addEventListener(
        'keydown',
        async e => {
            const video =
                activeVideo ||
                getCurrentVideo();

            if (!video) {
                return;
            }

            let handled = true;

            switch (e.code) {

                /**
                 * F
                 *
                 * normal -> fullscreen
                 */

                case 'KeyF': {
                    if (!isOverlayMode) {
                        await enterOverlayFullscreen(
                            video
                        );
                    }

                    break;
                }

                /**
                 * Space
                 */

                case 'Space': {
                    if (!isOverlayMode) {
                        handled = false;

                        break;
                    }

                    if (video.paused) {
                        try {
                            await video.play();
                        } catch (_) {}
                    }

                    else {
                        video.pause();
                    }

                    break;
                }

                /**
                 * ←
                 */

                case 'ArrowLeft': {
                    if (!isOverlayMode) {
                        handled = false;

                        break;
                    }

                    const current =
                        Number.isFinite(
                            video.currentTime
                        )
                            ? video.currentTime
                            : 0;

                    video.currentTime =
                        Math.max(
                            0,
                            current -
                            CONFIG.SEEK_STEP_SECONDS
                        );

                    break;
                }

                /**
                 * →
                 */

                case 'ArrowRight': {
                    if (!isOverlayMode) {
                        handled = false;

                        break;
                    }

                    const current =
                        Number.isFinite(
                            video.currentTime
                        )
                            ? video.currentTime
                            : 0;

                    if (
                        Number.isFinite(
                            video.duration
                        ) &&
                        video.duration > 0
                    ) {
                        video.currentTime =
                            Math.min(
                                video.duration,

                                current +
                                CONFIG.SEEK_STEP_SECONDS
                            );
                    }

                    else {
                        video.currentTime =
                            current +
                            CONFIG.SEEK_STEP_SECONDS;
                    }

                    break;
                }

                /**
                 * ↑
                 */

                case 'ArrowUp': {
                    if (!isOverlayMode) {
                        handled = false;

                        break;
                    }

                    video.volume =
                        clamp(
                            video.volume +
                            CONFIG.VOLUME_STEP,

                            0,

                            1
                        );

                    break;
                }

                /**
                 * ↓
                 */

                case 'ArrowDown': {
                    if (!isOverlayMode) {
                        handled = false;

                        break;
                    }

                    video.volume =
                        clamp(
                            video.volume -
                            CONFIG.VOLUME_STEP,

                            0,

                            1
                        );

                    break;
                }

                /**
                 * M
                 */

                case 'KeyM': {
                    if (!isOverlayMode) {
                        handled = false;

                        break;
                    }

                    video.muted =
                        !video.muted;

                    break;
                }

                /**
                 * Numpad 2
                 */

                case 'Numpad2': {
                    if (!isOverlayMode) {
                        handled = false;

                        break;
                    }

                    move(
                        0,
                        -CONFIG.PAN_STEP
                    );

                    break;
                }

                /**
                 * Numpad 8
                 */

                case 'Numpad8': {
                    if (!isOverlayMode) {
                        handled = false;

                        break;
                    }

                    move(
                        0,
                        CONFIG.PAN_STEP
                    );

                    break;
                }

                /**
                 * Numpad 6
                 */

                case 'Numpad6': {
                    if (!isOverlayMode) {
                        handled = false;

                        break;
                    }

                    move(
                        -CONFIG.PAN_STEP,
                        0
                    );

                    break;
                }

                /**
                 * Numpad 4
                 */

                case 'Numpad4': {
                    if (!isOverlayMode) {
                        handled = false;

                        break;
                    }

                    move(
                        CONFIG.PAN_STEP,
                        0
                    );

                    break;
                }

                /**
                 * Numpad 9
                 */

                case 'Numpad9': {
                    if (!isOverlayMode) {
                        handled = false;

                        break;
                    }

                    zoom(
                        CONFIG.ZOOM_STEP
                    );

                    break;
                }

                /**
                 * Numpad 1
                 */

                case 'Numpad1': {
                    if (!isOverlayMode) {
                        handled = false;

                        break;
                    }

                    zoom(
                        -CONFIG.ZOOM_STEP
                    );

                    break;
                }

                /**
                 * Numpad 5
                 */

                case 'Numpad5': {
                    if (!isOverlayMode) {
                        handled = false;

                        break;
                    }

                    resetTransform();

                    break;
                }

                default: {
                    handled = false;
                }
            }

            if (!handled) {
                return;
            }

            e.preventDefault();

            e.stopImmediatePropagation();

            updateControls();
        },
        true
    );

    /**
     * ============================================================
     * Fullscreen lifecycle
     * ============================================================
     */

    document.addEventListener(
        'fullscreenchange',
        () => {
            if (
                isOverlayMode &&
                !document.fullscreenElement
            ) {
                cleanupOverlay();
            }
        }
    );

    async function exitFullscreenSafely() {
        if (document.fullscreenElement) {
            try {
                await document.exitFullscreen();
            }

            catch (err) {
                console.warn(
                    '[IG Transformer] exitFullscreen failed:',
                    err
                );

                cleanupOverlay();
            }

            return;
        }

        cleanupOverlay();
    }

    /**
     * ============================================================
     * Normal mode scanner
     * ============================================================
     */

    function refreshNormalControls() {
        if (scanInProgress) {
            return;
        }

        if (isOverlayMode) {
            return;
        }

        scanInProgress = true;

        try {
            if (
                !CONFIG.NORMAL_CONTROLS_ENABLED
            ) {
                removeNormalControls();

                activeVideo = null;

                return;
            }

            const video =
                getCurrentVideo();

            if (!video) {
                removeNormalControls();

                activeVideo = null;

                return;
            }

            if (
                video !== activeVideo ||
                !normalBar ||
                !document.contains(
                    normalBar
                )
            ) {
                activeVideo = null;

                installNormalControls(
                    video
                );

                return;
            }

            activeVideo = video;

            positionNormalBar(
                video
            );

            updateControls();
        }

        finally {
            scanInProgress = false;
        }
    }

    function scheduleReposition() {
        if (isOverlayMode) {
            return;
        }

        if (repositionTimer) {
            return;
        }

        repositionTimer =
            window.setTimeout(
                () => {
                    repositionTimer =
                        null;

                    const video =
                        getCurrentVideo();

                    if (!video) {
                        removeNormalControls();

                        activeVideo =
                            null;

                        return;
                    }

                    if (
                        video !==
                        activeVideo
                    ) {
                        refreshNormalControls();

                        return;
                    }

                    positionNormalBar(
                        video
                    );

                    updateControls();
                },

                CONFIG
                    .REPOSITION_THROTTLE_MS
            );
    }

    /**
     * ============================================================
     * Page events
     * ============================================================
     */

    window.addEventListener(
        'resize',
        scheduleReposition
    );

    window.addEventListener(
        'scroll',
        scheduleReposition,
        true
    );

    document.addEventListener(
        'visibilitychange',
        () => {
            if (document.hidden) {
                return;
            }

            refreshNormalControls();
        }
    );

    /**
     * ============================================================
     * Start
     * ============================================================
     */

    setInterval(
        refreshNormalControls,
        CONFIG.SCAN_INTERVAL_MS
    );

    debug(
        'IG Transformer 1.2.1 loaded'
    );

    refreshNormalControls();

})();