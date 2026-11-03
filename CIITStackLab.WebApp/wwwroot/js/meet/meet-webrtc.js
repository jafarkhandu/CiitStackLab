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
    const audioMeters = new Map();
    let audioContext = null;
    let localAudioMeter = null;
    let activeSpeakerTile = null;
    let localStream = new MediaStream();
    let screenStream = null;
    let localVideo = grid.querySelector(".ciit-meet-video-self video");
    const selfTile = grid.querySelector(".ciit-meet-video-self");
    const waitingTile = grid.querySelector(".ciit-meet-video-empty");
    const localPlaceholder = selfTile?.querySelector(".ciit-meet-video-placeholder");
    const localName = selfTile?.querySelector(".ciit-meet-video-name");
    const micButton = room.querySelector('[data-room-toggle="mic"]');
    const cameraButton = room.querySelector('[data-room-toggle="camera"]');
    const shareButton = room.querySelector('[data-room-toggle="share"]');
    const mediaPermissions = {
        mic: true,
        camera: true,
        share: true
    };
    let requestMedia;

    const applyMediaPermissions = update => {
        if (!update || Number(update.sessionId) !== sessionId) return;
        const next = {
            mic: update.canUseMicrophone !== false,
            camera: update.canUseCamera !== false,
            share: update.canShareScreen !== false
        };
        Object.assign(mediaPermissions, next);
        const controls = [
            ["mic", micButton],
            ["camera", cameraButton],
            ["share", shareButton]
        ];
        controls.forEach(([kind, button]) => {
            if (button) {
                button.disabled = !mediaPermissions[kind];
                button.title = mediaPermissions[kind] ? "" : "The host disabled this control.";
            }
        });
        ["mic", "camera"].forEach(kind => {
            if (!mediaPermissions[kind]) {
                setTrackEnabled(kind, false).catch(error => console.warn("Device permission cleanup failed:", error));
                setToggleVisual(kind === "mic" ? micButton : cameraButton, kind, false);
                saveMediaPreference(kind, false);
            }
        });
        if (!mediaPermissions.share && screenStream) {
            screenStream.getTracks().forEach(track => track.stop());
            screenStream = null;
            setToggleVisual(shareButton, "share", false);
        }
    };
    window.ciitMeetApplyMediaPermissions = applyMediaPermissions;

    connection.on("MeetMyParticipantStatus", applyMediaPermissions);
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
                camera: saved.camera === true
            };
        } catch {
            return { mic: false, camera: false };
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
        const hasVideo = localStream.getVideoTracks().some(track => track.readyState === "live" && track.enabled);
        localVideo.srcObject = hasVideo ? localStream : null;
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
        if (pinnedTile) return;
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

    window.setInterval(updateActiveSpeaker, 180);

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

    const applyParticipantStreamState = peer => {
        const videoTrack = peer.remoteStream?.getVideoTracks().find(track => track.readyState === "live");
        const cameraActive = (peer.screenSharing === true || peer.cameraEnabled !== false)
            && Boolean(videoTrack && videoTrack.enabled && !videoTrack.muted);
        peer.tile.video.hidden = !cameraActive;
        peer.tile.placeholder.hidden = cameraActive;
        const detail = peer.tile.placeholder.querySelector("small");
        if (detail) detail.textContent = cameraActive ? "" : "Camera is off";
        peer.tile.tile.classList.toggle("ciit-meet-camera-off", !cameraActive);
    };

    const addLocalTracksToPeer = (peer) => {
        localStream.getTracks().forEach(track => {
            const kind = track.kind;
            const existing = peer.senders[kind];
            if (existing?.track?.id === track.id) return;

            if (existing && existing.track === null) {
                existing.replaceTrack(track).catch(error => console.warn("Local track attach failed:", error));
            } else {
                peer.senders[kind] = peer.connection.addTrack(track, localStream);
            }
        });
    };

    const createRemoteTile = (peerId, displayName = "Participant") => {
        const tile = document.createElement("article");
        tile.className = "ciit-meet-video-tile ciit-meet-video-remote";
        tile.dataset.peerConnectionId = peerId;

        const video = document.createElement("video");
        video.autoplay = true;
        video.playsInline = true;
        video.muted = false;
        video.setAttribute("aria-label", "Meeting participant video");
        video.addEventListener("loadedmetadata", () => video.play().catch(() => {}));

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

        tile.append(video, placeholder, shareBadge, nameBadge, pin);
        grid.append(tile);
        if (waitingTile) waitingTile.hidden = true;
        return { tile, video, placeholder, pin };
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
        if (pinnedTile === peer.tile.tile) {
            pinnedTile = null;
            grid.classList.remove("has-pinned-tile");
        }
        peer.connection.ontrack = null;
        peer.connection.onicecandidate = null;
        peer.connection.onnegotiationneeded = null;
        peer.connection.close();
        const meter = audioMeters.get(peerId);
        meter?.source.disconnect();
        audioMeters.delete(peerId);
        peer.tile.tile.remove();
        peers.delete(peerId);
        if (grid.querySelectorAll(".ciit-meet-video-remote").length === 0 && waitingTile) {
            waitingTile.hidden = false;
        }
    };

    const sendSignal = (peerId, signalType, signalData) =>
        connection.invoke("RelayWebRtcSignal", sessionId, peerId, signalType, signalData);

    const createPeer = (peerId, displayName = "Participant") => {
        if (!peerId || peerId === connection.connectionId) return null;
        if (peers.has(peerId)) {
            const existing = peers.get(peerId);
            if (displayName && displayName !== "Participant") {
                const label = existing.tile.tile.querySelector(".ciit-meet-video-name");
                if (label) label.textContent = displayName;
            }
            return existing;
        }

        const pc = new RTCPeerConnection({
            iceServers: []
        });
        const peer = {
            connection: pc,
            tile: createRemoteTile(peerId, displayName),
            senders: { audio: null, video: null },
            cameraEnabled: true,
            micEnabled: true,
            screenSharing: false,
            initDataChannelCreated: false,
            polite: String(connection.connectionId) > String(peerId),
            makingOffer: false,
            ignoreOffer: false,
            isSettingRemoteAnswerPending: false
        };
        peers.set(peerId, peer);

        pc.onicecandidate = event => {
            if (event.candidate) {
                sendSignal(peerId, "IceCandidate", event.candidate.toJSON())
                    .catch(error => console.warn("ICE relay failed:", error));
            }
        };

        pc.ontrack = event => {
            const stream = event.streams[0] || new MediaStream([event.track]);
            if (!peer.remoteStream) peer.remoteStream = new MediaStream();
            if (!peer.remoteStream.getTracks().some(track => track.id === event.track.id)) {
                peer.remoteStream.addTrack(event.track);
            }
            peer.tile.video.srcObject = peer.remoteStream;
                peer.tile.video.play().catch(() => {});
            if (event.track.kind === "audio" && !audioMeters.has(peerId)) {
                audioMeters.set(peerId, createAudioMeter(stream));
            }
            if (event.track.kind === "video") {
                const refreshRemoteVideoState = () => applyParticipantStreamState(peer);
                event.track.onmute = refreshRemoteVideoState;
                event.track.onunmute = refreshRemoteVideoState;
                event.track.onended = refreshRemoteVideoState;
                refreshRemoteVideoState();
            }
        };

        pc.onnegotiationneeded = async () => {
            try {
                peer.makingOffer = true;
                await pc.setLocalDescription();
                await sendSignal(peerId, pc.localDescription.type === "offer" ? "Offer" : "Answer", pc.localDescription);
            } catch (error) {
                console.error("WebRTC negotiation failed:", error);
            } finally {
                peer.makingOffer = false;
            }
        };

        pc.onconnectionstatechange = () => {
            if (pc.connectionState === "failed" || pc.connectionState === "closed") {
                removePeer(peerId);
            }
        };

        addLocalTracksToPeer(peer);
        return peer;
    };

    const handleSignal = async message => {
        if (!message || message.sessionId !== sessionId || message.senderConnectionId === connection.connectionId) return;
        const peer = createPeer(message.senderConnectionId);
        if (!peer) return;
        const pc = peer.connection;

        try {
            if (message.signalType === "IceCandidate") {
                if (message.signalData && !peer.ignoreOffer) {
                    await pc.addIceCandidate(message.signalData);
                }
                return;
            }

            const description = message.signalData;
            const readyForOffer = !peer.makingOffer
                && (pc.signalingState === "stable" || peer.isSettingRemoteAnswerPending);
            const offerCollision = description.type === "offer" && !readyForOffer;
            peer.ignoreOffer = !peer.polite && offerCollision;
            if (peer.ignoreOffer) return;

            if (offerCollision && peer.polite) {
                await pc.setLocalDescription({ type: "rollback" });
            }

            peer.isSettingRemoteAnswerPending = description.type === "answer";
            await pc.setRemoteDescription(description);
            peer.isSettingRemoteAnswerPending = false;

            if (description.type === "offer") {
                await pc.setLocalDescription();
                await sendSignal(message.senderConnectionId, "Answer", pc.localDescription);
            }
        } catch (error) {
            peer.isSettingRemoteAnswerPending = false;
            if (!peer.ignoreOffer) console.error("WebRTC signal handling failed:", error);
        }
    };

    requestMedia = async kind => {
        if (mediaCleanupCompleted) throw new Error("The meeting has ended.");
        const hasTrack = localStream.getTracks().some(track => track.kind === kind && track.readyState === "live");
        if (hasTrack) return;
        if (!navigator.mediaDevices?.getUserMedia) {
            toast("Device unavailable", "Camera and microphone access require a supported browser and HTTPS.");
            throw new Error("getUserMedia unavailable.");
        }

        const stream = await navigator.mediaDevices.getUserMedia({
            audio: kind === "audio",
            video: kind === "video"
        });
        if (mediaCleanupCompleted) {
            stream.getTracks().forEach(track => track.stop());
            throw new Error("The meeting has ended.");
        }
        stream.getTracks().forEach(track => {
            localStream.addTrack(track);
            if (track.kind === "audio") localAudioMeter = createAudioMeter(localStream);
            peers.forEach(peer => {
                const sender = peer.senders[kind];
                if (sender && sender.track === null) {
                    sender.replaceTrack(track).catch(error => console.warn("Device track attach failed:", error));
                } else {
                    peer.senders[kind] = peer.connection.addTrack(track, localStream);
                }
            });
        });
        updateLocalPreview();
    };

    const publishPeerMediaState = (mediaKind, enabled) => {
        connection.invoke("BroadcastPeerMediaState", sessionId, mediaKind, enabled)
            .catch(error => console.warn("Meet media state notification failed:", error));
    };

    const setTrackEnabled = async (kind, enabled) => {
        if (mediaCleanupCompleted) return;
        const mediaKind = kind === "mic" ? "audio" : "video";
        if (enabled && !mediaPermissions[kind]) {
            throw new Error("The host has disabled this device for you.");
        }

        const track = localStream.getTracks().find(item => item.kind === mediaKind && item.readyState === "live");
        if (enabled) {
            await requestMedia(mediaKind);
            if (mediaCleanupCompleted) return;
            const restoredTrack = localStream.getTracks().find(item => item.kind === mediaKind && item.readyState === "live");
            if (restoredTrack) {
                restoredTrack.enabled = true;
                if (mediaKind === "video") {
                    peers.forEach(peer => {
                        const sender = peer.senders.video;
                        if (sender && sender.track !== restoredTrack) {
                            sender.replaceTrack(restoredTrack).catch(error => console.warn("Camera track restore failed:", error));
                        }
                        applyParticipantStreamState(peer);
                    });
                }
            }
        } else if (track) {
            // Detach and stop the capture track. Replacing with a black frame would
            // falsely imply camera is still active; the remote video element receives track mute/end.
            peers.forEach(peer => {
                const sender = peer.senders[mediaKind];
                if (sender?.track?.id === track.id) {
                    sender.replaceTrack(null).catch(error => console.warn("Device track detach failed:", error));
                }
            });
            localStream.removeTrack(track);
            track.stop();
            if (mediaKind === "audio" && localAudioMeter) {
                localAudioMeter.source.disconnect();
                localAudioMeter = null;
            }
        }

        saveMediaPreference(kind, enabled);
        updateLocalPreview();
        updateSelfMicIndicator();
        publishPeerMediaState(kind === "mic" ? "mic" : "camera", enabled);
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

    shareButton?.addEventListener("click", async () => {
        const sharing = shareButton.getAttribute("aria-pressed") === "true";
        if (!sharing) {
            screenStream?.getTracks().forEach(track => track.stop());
            screenStream = null;
            publishPeerMediaState("share", false);
            const cameraTrack = localStream.getVideoTracks().find(track => track.readyState === "live");
            peers.forEach(peer => {
                const sender = peer.senders.video;
                if (sender) sender.replaceTrack(cameraTrack || null).catch(error => console.error(error));
            });
            setToggleVisual(shareButton, "share", false);
            updateLocalPreview();
            return;
        }

        if (!mediaPermissions.share) {
            setToggleVisual(shareButton, "share", false);
            toast("Screen sharing disabled", "The host has disabled screen sharing for you.");
            return;
        }

        try {
            if (mediaCleanupCompleted) return;
            if (!navigator.mediaDevices?.getDisplayMedia) throw new Error("Screen sharing is not supported in this browser.");
            screenStream = await navigator.mediaDevices.getDisplayMedia({ video: true, audio: false });
            if (mediaCleanupCompleted) {
                screenStream.getTracks().forEach(track => track.stop());
                screenStream = null;
                return;
            }
            const screenTrack = screenStream.getVideoTracks()[0];
            publishPeerMediaState("share", true);
            peers.forEach(peer => {
                const sender = peer.senders.video;
                if (sender) sender.replaceTrack(screenTrack).catch(error => console.error(error));
                else peer.senders.video = peer.connection.addTrack(screenTrack, screenStream);
            });
            if (localVideo) {
                localVideo.srcObject = screenStream;
                localVideo.hidden = false;
                if (localPlaceholder) localPlaceholder.hidden = true;
            }
            const shareLabel = shareButton.querySelector("span");
            const shareIcon = shareButton.querySelector("i");
            if (shareLabel) shareLabel.textContent = "Sharing screen";
            if (shareIcon) shareIcon.className = "bi bi-display-fill";
            screenTrack.onended = () => {
                shareButton.setAttribute("aria-pressed", "false");
                shareButton.classList.remove("is-active");
                const shareLabel = shareButton.querySelector("span");
                const shareIcon = shareButton.querySelector("i");
                if (shareLabel) shareLabel.textContent = "Share screen";
                if (shareIcon) shareIcon.className = "bi bi-display";
                screenStream = null;
                publishPeerMediaState("share", false);
                const cameraTrack = localStream.getVideoTracks().find(track => track.readyState === "live");
                peers.forEach(peer => {
                    const sender = peer.senders.video;
                    if (sender) sender.replaceTrack(cameraTrack || null).catch(error => console.error(error));
                });
                updateLocalPreview();
            };
        } catch (error) {
            setToggleVisual(shareButton, "share", false);
            screenStream?.getTracks().forEach(track => track.stop());
            screenStream = null;
            updateLocalPreview();
            toast("Screen share failed", error.message || "Screen sharing permission was not granted.");
        }
    });

    window.ciitMeetApplyMediaPermissions = update => {
        if (!update || room.dataset.isHost === "true") return;

        ["mic", "camera", "share"].forEach(type => {
            const property = type === "mic" ? "canUseMicrophone"
                : type === "camera" ? "canUseCamera"
                    : "canShareScreen";
            if (typeof update[property] !== "boolean") return;
            mediaPermissions[type] = update[property];

            const button = type === "mic" ? micButton
                : type === "camera" ? cameraButton
                    : shareButton;
            if (button) button.disabled = !mediaPermissions[type];

            if (!mediaPermissions[type] && type !== "share") {
                const mediaKind = type === "mic" ? "audio" : "video";
                setToggleVisual(button, type, false);
                setTrackEnabled(type, false).catch(error => console.warn("Host device permission cleanup failed:", error));
            }

            if (!mediaPermissions.share && screenStream) {
                screenStream.getTracks().forEach(track => track.stop());
                screenStream = null;
                setToggleVisual(shareButton, "share", false);
                const cameraTrack = localStream.getVideoTracks().find(track => track.readyState === "live");
                peers.forEach(peer => {
                    const sender = peer.senders.video;
                    if (sender) sender.replaceTrack(cameraTrack || null).catch(error => console.error(error));
                });
                updateLocalPreview();
            }
        });
    };

    connection.on("MeetExistingPeers", peerInfos => {
        (peerInfos || []).forEach(peerInfo => {
            const peerId = typeof peerInfo === "string" ? peerInfo : peerInfo?.connectionId;
            const displayName = typeof peerInfo === "string" ? "Participant" : peerInfo?.displayName || "Participant";
            const peer = createPeer(peerId, displayName);
            if (peer) {
                addLocalTracksToPeer(peer);
                if (localStream.getTracks().length === 0 && !peer.initDataChannelCreated) {
                    peer.connection.createDataChannel("ciit-meet-peer-init");
                    peer.initDataChannelCreated = true;
                }
            }
        });
    });
    connection.on("MeetPeerJoined", peerInfo => {
        const peerId = typeof peerInfo === "string" ? peerInfo : peerInfo?.connectionId;
        const displayName = typeof peerInfo === "string" ? "Participant" : peerInfo?.displayName || "Participant";
        createPeer(peerId, displayName);
    });
    connection.on("MeetPeerLeft", removePeer);
    connection.on("MeetPeerMediaState", state => {
        if (!state || Number(state.sessionId) !== sessionId || !state.connectionId) return;
        const peer = peers.get(state.connectionId);
        if (!peer) return;

        if (state.mediaKind === "camera") {
            peer.cameraEnabled = state.enabled === true;
            applyParticipantStreamState(peer);
            if (!peer.cameraEnabled) {
                peer.tile.video.pause();
                peer.tile.video.srcObject = null;
                peer.tile.video.hidden = true;
                peer.tile.placeholder.hidden = false;
                const detail = peer.tile.placeholder.querySelector("small");
                if (detail) detail.textContent = "Camera is off";
            } else {
                peer.tile.video.srcObject = peer.remoteStream || null;
                applyParticipantStreamState(peer);
                if (peer.tile.video.srcObject) {
                    peer.tile.video.play().catch(() => {});
                }
            }
        } else if (state.mediaKind === "mic") {
            peer.micEnabled = state.enabled === true;
            const indicator = peer.tile.tile.querySelector("[data-peer-mic-state]");
            if (indicator) {
                indicator.className = "bi " + (peer.micEnabled ? "bi-mic-fill" : "bi-mic-mute");
            }
        } else if (state.mediaKind === "share") {
            peer.screenSharing = state.enabled === true;
            peer.tile.tile.classList.toggle("ciit-meet-screen-sharing", peer.screenSharing);
            const badge = peer.tile.tile.querySelector("[data-peer-screen-share-state]");
            if (badge) badge.hidden = !peer.screenSharing;
            applyParticipantStreamState(peer);
        }
    });
    connection.on("MeetWebRtcSignal", handleSignal);

    connection.onreconnected(() => {
        peers.forEach((peer, peerId) => removePeer(peerId));
    });


    let mediaCleanupCompleted = false;
    window.ciitMeetWebRtcCleanup = () => {
        if (mediaCleanupCompleted) return;
        mediaCleanupCompleted = true;

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
        peers.forEach((peer, peerId) => removePeer(peerId));
        audioContext?.close().catch(() => {});
        audioContext = null;
        connection.invoke("LeavePeerRoom", sessionId).catch(() => {});
    };

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

    window.addEventListener("pagehide", window.ciitMeetWebRtcCleanup);

    window.ciitMeetSignalRReady?.then(async () => {
        if (stage.hidden) return;
        if (isHost || room.dataset.participantStatus === "Admitted") {
            // Peer membership is authenticated server-side by JoinMeetingRoom.
            updateLocalPreview();
            const saved = readMediaPreferences();
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