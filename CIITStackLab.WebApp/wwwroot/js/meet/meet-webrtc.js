document.addEventListener("DOMContentLoaded", () => {
    const room = document.querySelector("[data-meet-room]");
    const stage = room?.querySelector("[data-meet-room-stage]");
    const grid = room?.querySelector(".ciit-meet-room-video-grid");
    if (!room || !stage || !grid) return;

    const isHost = room.dataset.isHost === "true";
    const sessionId = Number(room.dataset.sessionId);
    const connection = window.ciitMeetSignalRConnection;
    if (!connection || !window.RTCPeerConnection) {
        console.error("CIIT Meet WebRTC could not initialize.");
        return;
    }

    const peers = new Map();
    const departedPeers = new Set();
    let peersBeforeSync = null;
    let peerGeneration = 0;
    let iceServers = [{ urls: "stun:stun.l.google.com:19302" }];
    let iceRequest = null;
    let iceRequestConnectionId = null;
    let lastIceRequestAt = 0;
    const prepareIce = (refresh = false) => {
        if (connection.state !== "Connected" || mediaCleanupCompleted) return Promise.resolve();
        const connectionId = connection.connectionId;
        if (iceRequest && iceRequestConnectionId === connectionId) return iceRequest;
        if (!refresh && iceRequestConnectionId === connectionId && Date.now() - lastIceRequestAt < 10000) return Promise.resolve();

        lastIceRequestAt = Date.now();
        iceRequestConnectionId = connectionId;
        const request = connection.invoke("GetIceServers", sessionId)
            .then(servers => {
                if (mediaCleanupCompleted || connectionId !== connection.connectionId) return;
                if (!Array.isArray(servers) || !servers.length || JSON.stringify(servers) === JSON.stringify(iceServers)) return;
                iceServers = servers;
                peers.forEach(peer => {
                    const pc = peer.connection;
                    pc.setConfiguration({ ...pc.getConfiguration(), iceServers });
                });
            })
            .catch(error => console.warn("Meet ICE configuration unavailable:", error))
            .finally(() => {
                if (iceRequest === request) iceRequest = null;
            });
        iceRequest = request;
        return request;
    };
    window.ciitMeetPrepareIce = prepareIce;

    const audioMeters = new Map();
    const pendingAudio = new Set();
    let audioContext = null;
    let localAudioMeter = null;
    let activeSpeakerTile = null;
    const localStream = new MediaStream();
    let screenStream = null;
    let localVideo = grid.querySelector(".ciit-meet-video-self video");
    const selfTile = grid.querySelector(".ciit-meet-video-self");
    const waitingTile = grid.querySelector(".ciit-meet-video-empty");
    const localPlaceholder = selfTile?.querySelector(".ciit-meet-video-placeholder");
    const micButton = room.querySelector('[data-room-toggle="mic"]');
    const cameraButton = room.querySelector('[data-room-toggle="camera"]');
    const shareButton = room.querySelector('[data-room-toggle="share"]');
    const enableAudioButton = room.querySelector("[data-meet-enable-audio]");
    const mediaPermissions = {
        mic: true,
        camera: true,
        share: true
    };
    const deviceRequests = { mic: 0, camera: 0, share: 0 };

    const updateAudioButton = () => {
        if (enableAudioButton) enableAudioButton.hidden = pendingAudio.size === 0;
    };

    const playRemoteAudio = audio => {
        if (!audio.srcObject) return;
        audio.play()?.then(() => {
            pendingAudio.delete(audio);
            updateAudioButton();
        }).catch(error => {
            if (error?.name === "NotAllowedError" && audio.srcObject) {
                pendingAudio.add(audio);
                updateAudioButton();
            } else if (error?.name !== "AbortError") {
                console.warn("Remote audio playback failed:", error);
            }
        });
    };

    const applyMediaPermissions = update => {
        if (!update || isHost || Number(update.sessionId) !== sessionId) return;
        const controls = [
            ["mic", micButton, update.canUseMicrophone],
            ["camera", cameraButton, update.canUseCamera],
            ["share", shareButton, update.canShareScreen]
        ];
        controls.forEach(([kind, button, allowed]) => {
            if (typeof allowed !== "boolean") return;
            const changed = mediaPermissions[kind] !== allowed;
            mediaPermissions[kind] = allowed;
            if (button) {
                button.disabled = !allowed;
                button.title = allowed ? "" : "The host disabled this control.";
            }
            if (allowed || !changed) return;
            if (kind === "share") stopScreenShare();
            else setTrackEnabled(kind, false).catch(error => console.warn("Device permission cleanup failed:", error));
        });
    };
    window.ciitMeetApplyMediaPermissions = applyMediaPermissions;

    const preferenceKey = "ciit-meet-media-" + sessionId;

    const setToggleVisual = (button, type, active) => {
        if (!button) return;
        button.setAttribute("aria-pressed", active ? "true" : "false");
        button.classList.toggle("is-active", active);
        const icon = button.querySelector("i");
        const label = button.querySelector("span");
        const states = {
            mic: active ? ["bi-mic-fill", "Mute"] : ["bi-mic-mute", "Unmute"],
            camera: active ? ["bi-camera-video-fill", "Stop video"] : ["bi-camera-video-off", "Start video"],
            share: active ? ["bi-display-fill", "Sharing screen"] : ["bi-display", "Share screen"]
        };
        if (icon) icon.className = "bi " + states[type][0];
        if (label) label.textContent = states[type][1];
    };

    const readMediaPreferences = () => {
        try {
            const saved = JSON.parse(sessionStorage.getItem(preferenceKey) || "{}");
            return {
                mic: saved.mic === true,
                camera: saved.camera === true,
                share: saved.share === true
            };
        } catch {
            return { mic: false, camera: false, share: false };
        }
    };

    const saveMediaPreference = (type, enabled) => {
        try {
            const saved = readMediaPreferences();
            saved[type] = enabled;
            sessionStorage.setItem(preferenceKey, JSON.stringify(saved));
        } catch {
            // Device controls remain usable when browser storage is unavailable.
        }
    };

    if (!localVideo && selfTile) {
        localVideo = document.createElement("video");
        localVideo.autoplay = true;
        localVideo.playsInline = true;
        localVideo.muted = true;
        localVideo.setAttribute("aria-label", "Your camera");
        selfTile.prepend(localVideo);
    }

    const toast = (title, message, type = "warning") => {
        if (typeof window.showMeetToast === "function") {
            window.showMeetToast({ title, message, type });
        } else {
            console.warn(title + ": " + message);
        }
    };

    const updateLocalPreview = () => {
        if (!localVideo) return;
        const preview = screenStream ?? localStream;
        const hasVideo = preview.getVideoTracks().some(track => track.readyState === "live" && track.enabled);
        localVideo.srcObject = hasVideo ? preview : null;
        localVideo.hidden = !hasVideo;
        if (localPlaceholder) localPlaceholder.hidden = hasVideo;
    };

    const ensureAudioContext = () => {
        if (!audioContext) {
            const AudioContextType = window.AudioContext || window.webkitAudioContext;
            if (AudioContextType) audioContext = new AudioContextType();
        }
        if (audioContext?.state === "suspended") audioContext.resume().catch(() => {});
        return audioContext;
    };

    enableAudioButton?.addEventListener("click", () => {
        ensureAudioContext();
        pendingAudio.forEach(playRemoteAudio);
    });

    const createAudioMeter = stream => {
        const context = ensureAudioContext();
        if (!context) return null;
        const source = context.createMediaStreamSource(stream);
        const analyser = context.createAnalyser();
        analyser.fftSize = 512;
        source.connect(analyser);
        return { source, analyser, data: new Uint8Array(analyser.fftSize) };
    };

    const readAudioLevel = meter => {
        if (!meter) return 0;
        meter.analyser.getByteTimeDomainData(meter.data);
        let sum = 0;
        for (const value of meter.data) {
            const normalized = (value - 128) / 128;
            sum += normalized * normalized;
        }
        return Math.sqrt(sum / meter.data.length);
    };

    const updateActiveSpeaker = () => {
        if (document.hidden || pinnedTile || audioContext?.state !== "running") return;
        let loudestTile = null;
        let loudestLevel = 0.035;
        const localLevel = readAudioLevel(localAudioMeter);
        if (localLevel > loudestLevel) {
            loudestLevel = localLevel;
            loudestTile = selfTile;
        }
        peers.forEach((peer, peerId) => {
            const level = readAudioLevel(audioMeters.get(peerId));
            if (level > loudestLevel) {
                loudestLevel = level;
                loudestTile = peer.tile.tile;
            }
        });
        if (activeSpeakerTile === loudestTile) return;
        activeSpeakerTile?.classList.remove("ciit-meet-video-active-speaker");
        activeSpeakerTile = loudestTile;
        activeSpeakerTile?.classList.add("ciit-meet-video-active-speaker");
    };

    const activeSpeakerTimer = window.setInterval(updateActiveSpeaker, 180);

    if (selfTile && !selfTile.querySelector(".ciit-meet-video-pin")) {
        const selfPin = document.createElement("button");
        selfPin.type = "button";
        selfPin.className = "ciit-meet-video-pin";
        selfPin.setAttribute("aria-label", "Pin your tile");
        selfPin.setAttribute("aria-pressed", "false");
        selfPin.innerHTML = '<i class="bi bi-pin-angle"></i>';
        selfPin.addEventListener("click", () => togglePinnedTile(selfTile, selfPin));
        selfTile.append(selfPin);
    }

    const setVideoTrack = (video, track) => {
        if (video.srcObject?.getVideoTracks()[0] !== track) {
            video.srcObject = track ? new MediaStream([track]) : null;
        }
    };

    const removeScreenTile = peer => {
        if (!peer.screenTile) return;
        peer.screenTile.video.srcObject = null;
        peer.screenTile.tile.remove();
        peer.screenTile = null;
    };

    const applyParticipantStreamState = peer => {
        const active = track => track.readyState === "live" && track.enabled && !track.muted;
        const tracks = [...peer.remoteVideoTracks.values()].filter(entry => active(entry.track));
        let screenTrack = tracks.find(entry => entry.isScreen && (peer.remoteScreenMid !== null
            ? peer.screenSharing !== false && entry.mid === peer.remoteScreenMid
            : entry.streamIds.includes(peer.remoteScreenStreamId)))?.track;
        if (!peer.hasScreenMetadata && peer.screenSharing) {
            // Older room scripts sent two video tracks without marking their streams.
            screenTrack = tracks[1]?.track ?? (peer.cameraEnabled === false ? tracks[0]?.track : undefined);
        }
        const cameraTrack = tracks.find(entry => !entry.isScreen && entry.track !== screenTrack)?.track;
        const cameraActive = peer.cameraEnabled !== false && Boolean(cameraTrack);
        setVideoTrack(peer.tile.video, cameraTrack);
        peer.tile.video.hidden = !cameraActive;
        peer.tile.placeholder.hidden = cameraActive;
        peer.tile.tile.classList.toggle("ciit-meet-camera-off", !cameraActive);

        if (screenTrack) {
            peer.screenTile ??= createRemoteScreenTile(peer.id, peer.tile.tile.dataset.peerDisplayName);
            setVideoTrack(peer.screenTile.video, screenTrack);
        } else {
            removeScreenTile(peer);
        }
        const badge = peer.tile.tile.querySelector("[data-peer-screen-share-state]");
        if (badge) badge.hidden = !screenTrack;
    };

    const addLocalTrackToPeer = (peer, track) => {
        if (!peer.localTracksReady) return;
        const sender = peer.senders[track.kind];
        if (sender) {
            if (sender.track !== track) {
                return sender.replaceTrack(track).catch(error => console.warn("Local track attach failed:", error));
            }
        } else {
            peer.senders[track.kind] = peer.connection.addTrack(track, localStream);
        }
    };

    const addScreenTrackToPeer = peer => {
        const track = screenStream?.getVideoTracks()[0];
        if (!peer.localTracksReady || !track) return;
        if (peer.screenTransceiver) {
            if (peer.screenSender.track !== track) {
                return peer.screenSender.replaceTrack(track).catch(error => console.warn("Screen track attach failed:", error));
            }
        } else if (!peer.screenSender) {
            peer.screenSender = peer.connection.addTrack(track, screenStream);
        }
    };

    const attachLocalTracks = peer => {
        if (peer.localTracksReady) return;
        peer.localTracksReady = true;
        return Promise.all([
            ...localStream.getTracks().filter(track => track.readyState === "live").map(track => addLocalTrackToPeer(peer, track)),
            addScreenTrackToPeer(peer)
        ]);
    };

    const useMediaTransceivers = (peer, audio, camera, screen) => {
        peer.senders.audio = audio.sender;
        peer.senders.video = camera.sender;
        peer.screenSender = screen.sender;
        peer.screenTransceiver = screen;
    };

    const createMediaTransceivers = peer => {
        // Reserve a slot per source so capture can start or stop without a new offer.
        const add = (kind, stream) => {
            const track = stream?.getTracks().find(item => item.kind === kind && item.readyState === "live");
            return peer.connection.addTransceiver(track ?? kind, {
                direction: "sendrecv",
                streams: track ? [stream] : []
            });
        };
        useMediaTransceivers(peer, add("audio", localStream), add("video", localStream), add("video", screenStream));
        peer.localTracksReady = true;
    };

    const matchMediaTransceivers = peer => {
        if (peer.remoteScreenMid === null) return;
        const transceivers = peer.connection.getTransceivers();
        const audio = transceivers.find(item => item.receiver.track.kind === "audio");
        const screen = transceivers.find(item => item.receiver.track.kind === "video" && item.mid === peer.remoteScreenMid);
        const camera = transceivers.find(item => item.receiver.track.kind === "video" && item !== screen);
        if (!audio || !screen || !camera) return;
        audio.direction = camera.direction = screen.direction = "sendrecv";
        useMediaTransceivers(peer, audio, camera, screen);
    };

    const createRemoteTile = (peerId, displayName = "Participant") => {
        const tile = document.createElement("article");
        tile.className = "ciit-meet-video-tile ciit-meet-video-remote";
        tile.dataset.peerConnectionId = peerId;
        tile.dataset.peerDisplayName = displayName;

        const video = document.createElement("video");
        video.autoplay = true;
        video.playsInline = true;
        video.muted = true;
        video.hidden = true;
        video.setAttribute("aria-label", "Meeting participant video");

        const audio = document.createElement("audio");
        audio.autoplay = true;
        audio.hidden = true;

        const placeholder = document.createElement("div");
        placeholder.className = "ciit-meet-video-placeholder";
        const avatar = document.createElement("span");
        avatar.className = "ciit-meet-avatar ciit-meet-avatar-muted";
        avatar.textContent = (displayName || "P").trim().charAt(0).toUpperCase();
        const participantLabel = document.createElement("strong");
        participantLabel.textContent = displayName;
        const detail = document.createElement("small");
        detail.textContent = "Camera is off";
        placeholder.append(avatar, participantLabel, detail);

        const pin = document.createElement("button");
        pin.type = "button";
        pin.className = "ciit-meet-video-pin";
        pin.setAttribute("aria-label", "Pin participant");
        pin.setAttribute("aria-pressed", "false");
        pin.innerHTML = '<i class="bi bi-pin-angle"></i>';
        pin.addEventListener("click", () => togglePinnedTile(tile, pin));

        const raisedHandBadge = document.createElement("span");
        raisedHandBadge.className = "ciit-meet-raised-hand ciit-meet-raised-hand-remote";
        raisedHandBadge.hidden = true;
        raisedHandBadge.setAttribute("aria-label", displayName + " has raised the hand");
        raisedHandBadge.innerHTML = '<i class="bi bi-hand-index-thumb-fill"></i>';

        const shareBadge = document.createElement("span");
        shareBadge.className = "ciit-meet-screen-share-badge";
        shareBadge.dataset.peerScreenShareState = "true";
        shareBadge.hidden = true;
        shareBadge.innerHTML = '<i class="bi bi-display"></i> Sharing screen';

        const nameBadge = document.createElement("span");
        nameBadge.className = "ciit-meet-video-name";
        const micState = document.createElement("i");
        micState.className = "bi bi-mic-fill";
        micState.dataset.peerMicState = "true";
        nameBadge.append(document.createTextNode(displayName + " "), micState);

        tile.append(video, audio, placeholder, shareBadge, raisedHandBadge, nameBadge, pin);
        grid.append(tile);
        if (waitingTile) waitingTile.hidden = true;
        return { tile, video, audio, placeholder };
    };

    const createRemoteScreenTile = (peerId, displayName) => {
        const tile = document.createElement("article");
        tile.className = "ciit-meet-video-tile ciit-meet-video-remote ciit-meet-screen-sharing";
        tile.dataset.peerConnectionId = peerId + "-screen";
        tile.dataset.peerDisplayName = displayName;
        const video = document.createElement("video");
        video.autoplay = true;
        video.playsInline = true;
        video.muted = true;
        video.setAttribute("aria-label", displayName + " screen share");
        const label = document.createElement("span");
        label.className = "ciit-meet-video-name";
        label.textContent = displayName + " screen";
        tile.append(video, label);
        grid.append(tile);
        if (waitingTile) waitingTile.hidden = true;
        return { tile, video };
    };

    let pinnedTile = null;
    const togglePinnedTile = (tile, button) => {
        if (pinnedTile === tile) {
            pinnedTile = null;
            tile.classList.remove("ciit-meet-video-pinned");
            button?.setAttribute("aria-pressed", "false");
            button?.setAttribute("aria-label", "Pin participant");
            grid.classList.remove("has-pinned-tile");
        } else {
            grid.querySelectorAll(".ciit-meet-video-pinned").forEach(item => item.classList.remove("ciit-meet-video-pinned"));
            grid.querySelectorAll(".ciit-meet-video-pin[aria-pressed='true']").forEach(item => {
                item.setAttribute("aria-pressed", "false");
                item.setAttribute("aria-label", "Pin participant");
            });
            pinnedTile = tile;
            tile.classList.add("ciit-meet-video-pinned");
            button?.setAttribute("aria-pressed", "true");
            button?.setAttribute("aria-label", "Unpin participant");
            grid.classList.add("has-pinned-tile");
        }
    };

    const removePeer = peerId => {
        const peer = peers.get(peerId);
        if (!peer) return;
        peers.delete(peerId);
        if (pinnedTile === peer.tile.tile) {
            pinnedTile = null;
            grid.classList.remove("has-pinned-tile");
        }
        window.clearTimeout(peer.recoveryTimer);
        peer.connection.ontrack = null;
        peer.connection.onicecandidate = null;
        peer.connection.onnegotiationneeded = null;
        peer.connection.oniceconnectionstatechange = null;
        peer.connection.close();
        audioMeters.get(peerId)?.source.disconnect();
        audioMeters.delete(peerId);
        peer.tile.audio.srcObject = null;
        peer.tile.video.srcObject = null;
        pendingAudio.delete(peer.tile.audio);
        updateAudioButton();
        removeScreenTile(peer);
        peer.tile.tile.remove();
        if (waitingTile && !peers.size) waitingTile.hidden = false;
    };

    const sendSignal = (peerId, signalType, signalData) =>
        connection.invoke("RelayWebRtcSignal", sessionId, peerId, signalType, signalData);

    const isCurrentPeer = peer => !mediaCleanupCompleted && peers.get(peer.id) === peer;

    // A description and its queued candidates must be applied before handling
    // the next offer, answer, or negotiationneeded event for this peer.
    const enqueuePeerSignal = (peer, operation) => {
        peer.signalQueue = peer.signalQueue.then(() => {
            if (isCurrentPeer(peer)) return operation();
        }).catch(error => {
            if (isCurrentPeer(peer) && connection.state === "Connected")
                console.warn("WebRTC signaling failed:", error);
        });
    };

    const sendLocalDescription = async peer => {
        const pc = peer.connection;
        const screenStreamId = screenStream?.id ?? null;
        await pc.setLocalDescription();
        if (!isCurrentPeer(peer) || connection.state !== "Connected") return;
        const { type, sdp } = pc.localDescription;
        const screenMid = peer.screenTransceiver?.mid;
        await sendSignal(peer.id, type === "offer" ? "Offer" : "Answer", screenMid == null
            ? { type, sdp, screenStreamId }
            : { type, sdp, screenMid });
    };

    const scheduleIceRecovery = (peer, delay) => {
        if (peer.recoveryTimer || peer.restartAttempts >= 3 || connection.state !== "Connected") return;
        peer.recoveryTimer = window.setTimeout(async () => {
            peer.recoveryTimer = null;
            const pc = peer.connection;
            if (!isCurrentPeer(peer) || connection.state !== "Connected"
                || ["connected", "completed", "closed"].includes(pc.iceConnectionState)) return;

            peer.restartAttempts += 1;
            await prepareIce();
            if (!isCurrentPeer(peer) || connection.state !== "Connected"
                || ["connected", "completed", "closed"].includes(pc.iceConnectionState)) return;

            // restartIce schedules negotiationneeded. That event is the sole
            // offer path, including when another local change is queued.
            try {
                pc.restartIce();
            } catch (error) {
                console.warn("Meet ICE restart failed:", error);
            }
            scheduleIceRecovery(peer, 8000 * peer.restartAttempts);
        }, delay);
    };

    const createPeer = (peerId, displayName = "Participant") => {
        if (!peerId || peerId === connection.connectionId || departedPeers.has(peerId) || mediaCleanupCompleted) return null;
        const existing = peers.get(peerId);
        if (existing) {
            if (displayName !== "Participant" && existing.tile.tile.dataset.peerDisplayName !== displayName) {
                existing.tile.tile.dataset.peerDisplayName = displayName;
                const label = existing.tile.tile.querySelector(".ciit-meet-video-name");
                if (label?.firstChild) label.firstChild.textContent = displayName + " ";
                const placeholder = existing.tile.placeholder;
                placeholder.querySelector("strong").textContent = displayName;
                placeholder.querySelector("span").textContent = displayName.trim().charAt(0).toUpperCase();
                removeScreenTile(existing);
                applyParticipantStreamState(existing);
            }
            return existing;
        }

        const pc = new RTCPeerConnection({ iceServers });
        const peer = {
            id: peerId,
            connection: pc,
            tile: createRemoteTile(peerId, displayName),
            localTracksReady: false,
            senders: { audio: null, video: null },
            cameraEnabled: true,
            screenSharing: null,
            polite: String(connection.connectionId) > String(peerId),
            signalQueue: Promise.resolve(),
            ignoreOffer: false,
            pendingCandidates: [],
            remoteIceFragments: new Set(),
            ignoredIceFragments: new Set(),
            remoteVideoTracks: new Map(),
            remoteScreenMid: null,
            remoteScreenStreamId: null,
            hasScreenMetadata: false,
            screenSender: null,
            screenTransceiver: null,
            screenTile: null,
            recoveryTimer: null,
            restartAttempts: 0
        };
        peers.set(peerId, peer);

        pc.onicecandidate = event => {
            if (event.candidate && connection.state === "Connected") {
                sendSignal(peerId, "IceCandidate", event.candidate.toJSON())
                    .catch(error => {
                        if (isCurrentPeer(peer) && connection.state === "Connected")
                            console.warn("ICE relay failed:", error);
                    });
            }
        };

        pc.ontrack = event => {
            const track = event.track;
            if (track.kind === "audio") {
                const audio = peer.tile.audio;
                const stream = new MediaStream([track]);
                audio.srcObject = stream;
                audioMeters.get(peerId)?.source.disconnect();
                audioMeters.set(peerId, createAudioMeter(stream));
                const play = () => {
                    if (audio.srcObject === stream) playRemoteAudio(audio);
                };
                track.onunmute = play;
                track.onended = () => {
                    if (audio.srcObject !== stream) return;
                    audio.srcObject = null;
                    audioMeters.get(peerId)?.source.disconnect();
                    audioMeters.delete(peerId);
                    pendingAudio.delete(audio);
                    updateAudioButton();
                };
                if (!track.muted) play();
            } else if (track.kind === "video") {
                const streamIds = event.streams.map(stream => stream.id);
                const mid = event.transceiver.mid;
                const isScreen = (mid !== null && mid === peer.remoteScreenMid) || streamIds.includes(peer.remoteScreenStreamId);
                peer.remoteVideoTracks.set(track.id, { track, mid, streamIds, isScreen });
                const updateVideo = () => {
                    if (isCurrentPeer(peer)) applyParticipantStreamState(peer);
                };
                track.onmute = updateVideo;
                track.onunmute = updateVideo;
                track.onended = () => {
                    peer.remoteVideoTracks.delete(track.id);
                    updateVideo();
                };
                updateVideo();
            }
        };

        pc.onnegotiationneeded = () => enqueuePeerSignal(peer, () => {
            if (pc.signalingState === "stable" && connection.state === "Connected") return sendLocalDescription(peer);
        });

        pc.oniceconnectionstatechange = () => {
            const state = pc.iceConnectionState;
            if (state === "connected" || state === "completed") {
                window.clearTimeout(peer.recoveryTimer);
                peer.recoveryTimer = null;
                peer.restartAttempts = 0;
            } else if (state === "failed") {
                window.clearTimeout(peer.recoveryTimer);
                peer.recoveryTimer = null;
                scheduleIceRecovery(peer, peer.restartAttempts === 0 ? 0 : 8000 * peer.restartAttempts);
            } else if (state === "disconnected") {
                scheduleIceRecovery(peer, 3500);
            }
        };

        return peer;
    };

    const iceFragments = sdp => new Set([...sdp.matchAll(/^a=ice-ufrag:([^\s]+)/gm)].map(match => match[1]));

    const candidateFragment = candidate =>
        candidate.usernameFragment ?? candidate.candidate?.match(/(?:^|\s)ufrag (\S+)/)?.[1];

    const addRemoteCandidate = async (peer, candidate) => {
        const fragment = candidateFragment(candidate);
        const matchesDescription = fragment && peer.remoteIceFragments.has(fragment);
        if (!matchesDescription && (fragment ? peer.ignoredIceFragments.has(fragment) : peer.ignoreOffer)) return;
        if (!peer.connection.remoteDescription || (fragment && !matchesDescription)) {
            // A candidate for an ICE restart can precede its description even
            // when the previous generation already has a remote description.
            if (peer.pendingCandidates.length >= 128) peer.pendingCandidates.shift();
            peer.pendingCandidates.push(candidate);
            return;
        }
        try {
            await peer.connection.addIceCandidate(candidate);
        } catch (error) {
            console.warn("Remote ICE candidate could not be applied:", error);
        }
    };

    const handleSignal = message => {
        if (!message || Number(message.sessionId) !== sessionId) return;
        const peer = createPeer(message.senderConnectionId);
        if (!peer) return;
        const pc = peer.connection;
        const data = message.signalData;
        if (!data) return;

        enqueuePeerSignal(peer, async () => {
            if (message.signalType === "IceCandidate") {
                await addRemoteCandidate(peer, data);
                return;
            }

            if (typeof data.sdp !== "string" || (data.type !== "offer" && data.type !== "answer")) return;
            const offerCollision = data.type === "offer" && pc.signalingState !== "stable";
            const fragments = iceFragments(data.sdp);
            peer.ignoreOffer = offerCollision && !peer.polite;
            if (peer.ignoreOffer) {
                peer.ignoredIceFragments = fragments;
                return;
            }
            if (data.type === "answer" && pc.signalingState !== "have-local-offer") return;

            if (offerCollision) await pc.setLocalDescription({ type: "rollback" });
            peer.remoteScreenMid = typeof data.screenMid === "string" ? data.screenMid : null;
            peer.hasScreenMetadata = peer.remoteScreenMid !== null || Object.hasOwn(data, "screenStreamId");
            peer.remoteScreenStreamId = data.screenStreamId ?? null;
            await pc.setRemoteDescription({ type: data.type, sdp: data.sdp });
            if (!isCurrentPeer(peer)) return;
            peer.remoteIceFragments = fragments;
            applyParticipantStreamState(peer);
            for (const candidate of peer.pendingCandidates.splice(0)) {
                await addRemoteCandidate(peer, candidate);
            }

            if (data.type === "offer") {
                if (!peer.localTracksReady) matchMediaTransceivers(peer);
                await attachLocalTracks(peer);
                await sendLocalDescription(peer);
            }
        });
    };

    const publishPeerMediaState = (mediaKind, enabled) => {
        if (connection.state !== "Connected" || mediaCleanupCompleted) return;
        return connection.invoke("BroadcastPeerMediaState", sessionId, mediaKind, enabled)
            .catch(error => console.warn("Meet media state notification failed:", error));
    };

    const setTrackEnabled = async (kind, enabled) => {
        if (mediaCleanupCompleted) return;
        const mediaKind = kind === "mic" ? "audio" : "video";
        const requestId = ++deviceRequests[kind];
        const existingTrack = localStream.getTracks().find(track => track.kind === mediaKind);
        if (enabled) {
            if (!mediaPermissions[kind]) throw new Error("The host has disabled this device for you.");
            if (existingTrack?.readyState !== "live") {
                if (!navigator.mediaDevices?.getUserMedia)
                    throw new Error("Camera and microphone access require a supported browser and HTTPS.");
                let stream;
                try {
                    stream = await navigator.mediaDevices.getUserMedia({
                        audio: mediaKind === "audio",
                        video: mediaKind === "video"
                    });
                } catch (error) {
                    if (mediaCleanupCompleted || requestId !== deviceRequests[kind]) return;
                    throw error;
                }
                if (mediaCleanupCompleted || requestId !== deviceRequests[kind] || !mediaPermissions[kind]) {
                    stream.getTracks().forEach(track => track.stop());
                    return;
                }
                if (existingTrack) localStream.removeTrack(existingTrack);
                const track = stream.getTracks().find(item => item.kind === mediaKind);
                if (!track) throw new Error("The selected device did not return a media track.");
                localStream.addTrack(track);
                if (mediaKind === "audio") {
                    localAudioMeter?.source.disconnect();
                    localAudioMeter = createAudioMeter(localStream);
                }
                peers.forEach(peer => addLocalTrackToPeer(peer, track));
                track.onended = () => {
                    if (localStream.getTracks().includes(track)) {
                        setTrackEnabled(kind, false).catch(error => console.warn("Device cleanup failed:", error));
                    }
                };
            }
        } else if (existingTrack) {
            peers.forEach(peer => {
                const sender = peer.senders[mediaKind];
                if (sender) sender.replaceTrack(null).catch(error => console.warn("Device track detach failed:", error));
            });
            localStream.removeTrack(existingTrack);
            existingTrack.stop();
            if (mediaKind === "audio") {
                localAudioMeter?.source.disconnect();
                localAudioMeter = null;
            }
        }

        setToggleVisual(kind === "mic" ? micButton : cameraButton, kind, enabled);
        saveMediaPreference(kind, enabled);
        updateLocalPreview();
        updateSelfMicIndicator();
        publishPeerMediaState(kind, enabled);
    };

    const updateSelfMicIndicator = () => {
        const indicator = selfTile?.querySelector(".ciit-meet-video-name i");
        const micTrack = localStream.getAudioTracks().find(track => track.readyState === "live" && track.enabled);
        if (indicator) indicator.className = "bi " + (micTrack ? "bi-mic-fill" : "bi-mic-mute");
    };

    micButton?.addEventListener("click", async () => {
        const enabled = micButton.getAttribute("aria-pressed") === "true";
        try {
            await setTrackEnabled("mic", enabled);
        } catch (error) {
            await setTrackEnabled("mic", false).catch(() => {});
            setToggleVisual(micButton, "mic", false);
            saveMediaPreference("mic", false);
            toast("Microphone access failed", error.message || "Allow microphone permission in your browser.");
        }
    });

    cameraButton?.addEventListener("click", async () => {
        const enabled = cameraButton.getAttribute("aria-pressed") === "true";
        try {
            await setTrackEnabled("camera", enabled);
        } catch (error) {
            await setTrackEnabled("camera", false).catch(() => {});
            setToggleVisual(cameraButton, "camera", false);
            saveMediaPreference("camera", false);
            toast("Camera access failed", error.message || "Allow camera permission in your browser.");
        }
    });

    const stopScreenShare = () => {
        deviceRequests.share += 1;
        const previousScreen = screenStream;
        screenStream = null;
        if (previousScreen) {
            previousScreen.getTracks().forEach(track => track.stop());
            peers.forEach(peer => {
                if (!peer.screenSender) return;
                if (peer.screenTransceiver) {
                    peer.screenSender.replaceTrack(null).catch(error => console.warn("Screen track detach failed:", error));
                } else {
                    peer.connection.removeTrack(peer.screenSender);
                    peer.screenSender = null;
                }
            });
            publishPeerMediaState("share", false);
        }
        saveMediaPreference("share", false);
        setToggleVisual(shareButton, "share", false);
        updateLocalPreview();
    };

    shareButton?.addEventListener("click", async () => {
        const sharing = shareButton.getAttribute("aria-pressed") === "true";
        if (!sharing) {
            stopScreenShare();
            return;
        }

        if (!mediaPermissions.share) {
            stopScreenShare();
            toast("Screen sharing disabled", "The host has disabled screen sharing for you.");
            return;
        }

        const requestId = ++deviceRequests.share;
        try {
            if (mediaCleanupCompleted) return;
            if (!navigator.mediaDevices?.getDisplayMedia)
                throw new Error("Screen sharing is not supported in this browser.");
            const stream = await navigator.mediaDevices.getDisplayMedia({ video: true, audio: false });
            if (mediaCleanupCompleted || requestId !== deviceRequests.share || !mediaPermissions.share) {
                stream.getTracks().forEach(track => track.stop());
                return;
            }
            const screenTrack = stream.getVideoTracks()[0];
            if (!screenTrack) throw new Error("The selected screen did not return a video track.");
            screenStream = stream;
            screenTrack.onended = () => {
                if (screenStream === stream) stopScreenShare();
            };
            peers.forEach(addScreenTrackToPeer);
            publishPeerMediaState("share", true);
            saveMediaPreference("share", true);
            setToggleVisual(shareButton, "share", true);
            updateLocalPreview();
        } catch (error) {
            if (requestId !== deviceRequests.share) return;
            stopScreenShare();
            toast("Screen share failed", error.message || "Screen sharing permission was not granted.");
        }
    });

    const runPeerEvent = operation => {
        if (mediaCleanupCompleted) return;
        if (!iceRequest) {
            operation();
            return;
        }
        const generation = peerGeneration;
        const connectionId = connection.connectionId;
        iceRequest.then(() => {
            if (!mediaCleanupCompleted && generation === peerGeneration && connectionId === connection.connectionId)
                operation();
        }).catch(error => console.warn("Meet peer update failed:", error));
    };

    const peerInfoId = peerInfo => typeof peerInfo === "string" ? peerInfo : peerInfo?.connectionId;
    const peerInfoName = peerInfo => typeof peerInfo === "string" ? "Participant" : peerInfo?.displayName || "Participant";

    window.ciitMeetBeginPeerSync = () => { peersBeforeSync = new Set(peers.keys()); };
    connection.on("MeetExistingPeers", peerInfos => runPeerEvent(() => {
        const snapshot = Array.isArray(peerInfos) ? peerInfos : [];
        const snapshotIds = new Set(snapshot.map(peerInfoId));
        peersBeforeSync?.forEach(peerId => {
            if (!snapshotIds.has(peerId)) {
                departedPeers.add(peerId);
                removePeer(peerId);
            }
        });
        peersBeforeSync = null;
        snapshot.forEach(peerInfo => {
            const peer = createPeer(peerInfoId(peerInfo), peerInfoName(peerInfo));
            if (!peer || peer.localTracksReady) return;
            // Joining browsers offer first. Incumbents match the offered slots.
            createMediaTransceivers(peer);
        });
    }));
    connection.on("MeetPeerJoined", peerInfo => runPeerEvent(() => {
        const peerId = peerInfoId(peerInfo);
        peersBeforeSync?.delete(peerId);
        departedPeers.delete(peerId);
        createPeer(peerId, peerInfoName(peerInfo));
    }));
    connection.on("MeetPeerLeft", peerId => runPeerEvent(() => {
        departedPeers.add(peerId);
        removePeer(peerId);
    }));
    connection.on("MeetPeerMediaState", state => runPeerEvent(() => {
        if (!state || Number(state.sessionId) !== sessionId || !state.connectionId) return;
        const peer = peers.get(state.connectionId);
        if (!peer) return;

        if (state.mediaKind === "camera") {
            peer.cameraEnabled = state.enabled === true;
            applyParticipantStreamState(peer);
        } else if (state.mediaKind === "mic") {
            const indicator = peer.tile.tile.querySelector("[data-peer-mic-state]");
            if (indicator) indicator.className = "bi " + (state.enabled === true ? "bi-mic-fill" : "bi-mic-mute");
        } else if (state.mediaKind === "share" && (peer.remoteScreenMid !== null || !peer.hasScreenMetadata)) {
            peer.screenSharing = state.enabled === true;
            applyParticipantStreamState(peer);
        }
    }));
    connection.on("MeetWebRtcSignal", signal => runPeerEvent(() => handleSignal(signal)));

    window.ciitMeetResetPeers = () => {
        peerGeneration += 1;
        peersBeforeSync = null;
        departedPeers.clear();
        peers.forEach((_, peerId) => removePeer(peerId));
    };

    window.ciitMeetPublishMediaState = () => Promise.all([
        publishPeerMediaState("mic", localStream.getAudioTracks().some(track => track.readyState === "live" && track.enabled)),
        publishPeerMediaState("camera", localStream.getVideoTracks().some(track => track.readyState === "live" && track.enabled)),
        publishPeerMediaState("share", Boolean(screenStream))
    ]);

    let mediaCleanupCompleted = false;
    const cleanUpMedia = (preserveSharePreference = false) => {
        if (mediaCleanupCompleted) return;
        mediaCleanupCompleted = true;
        window.clearInterval(activeSpeakerTimer);
        if (!preserveSharePreference) saveMediaPreference("share", false);

        // Stop every capture track immediately so the browser releases the
        // microphone/camera indicators when the meeting ends or the page closes.
        screenStream?.getTracks().forEach(track => track.stop());
        screenStream = null;
        localStream.getTracks().forEach(track => track.stop());
        localStream.getTracks().forEach(track => localStream.removeTrack(track));

        if (localVideo) {
            localVideo.pause();
            localVideo.srcObject = null;
            localVideo.hidden = true;
        }
        if (localPlaceholder) localPlaceholder.hidden = false;

        localAudioMeter?.source.disconnect();
        localAudioMeter = null;
        window.ciitMeetResetPeers();
        audioContext?.close().catch(() => {});
        audioContext = null;
        if (connection.state === "Connected") connection.invoke("LeavePeerRoom", sessionId).catch(() => {});
    };
    window.ciitMeetWebRtcCleanup = cleanUpMedia;

    // The ended screen is shown by both the host's successful End action and
    // the live session-status update received by participants. Observe that
    // single lifecycle signal so media is released in either path.
    const endedScreen = room.querySelector("[data-meet-ended-screen]");
    if (endedScreen) {
        const endedScreenObserver = new MutationObserver(() => {
            if (!endedScreen.hidden) {
                window.ciitMeetWebRtcCleanup();
                endedScreenObserver.disconnect();
            }
        });
        endedScreenObserver.observe(endedScreen, { attributes: true, attributeFilter: ["hidden"] });
        if (!endedScreen.hidden) {
            window.ciitMeetWebRtcCleanup();
            endedScreenObserver.disconnect();
        }
    }

    window.addEventListener("pagehide", event => {
        if (!event.persisted) cleanUpMedia(true);
    });

    window.ciitMeetSignalRReady?.then(async () => {
        if (stage.hidden || mediaCleanupCompleted) return;
        if (isHost || room.dataset.participantStatus === "Admitted") {
            // Peer membership is authenticated server-side by JoinMeetingRoom.
            updateLocalPreview();
            const saved = readMediaPreferences();
            if (saved.share && mediaPermissions.share) {
                saveMediaPreference("share", false);
                toast("Screen sharing paused", "Click Share screen to pick a screen again.", "info");
            }
            for (const type of ["mic", "camera"]) {
                if (!saved[type] || !mediaPermissions[type]) continue;
                const button = type === "mic" ? micButton : cameraButton;
                setToggleVisual(button, type, true);
                try {
                    await setTrackEnabled(type, true);
                } catch (error) {
                    setToggleVisual(button, type, false);
                    saveMediaPreference(type, false);
                    toast(type === "mic" ? "Microphone restore failed" : "Camera restore failed",
                        error.message || "Allow device access in your browser.");
                }
            }
        }
    }).catch(error => console.error("Meet WebRTC startup failed:", error));
});
