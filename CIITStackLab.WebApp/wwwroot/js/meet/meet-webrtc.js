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

    const addLocalTracksToPeer = (peer) => {
        localStream.getTracks().forEach(track => {
            if (!peer.connection.getSenders().some(sender => sender.track?.id === track.id)) {
                peer.connection.addTrack(track, localStream);
            }
        });
    };

    const createRemoteTile = peerId => {
        const tile = document.createElement("article");
        tile.className = "ciit-meet-video-tile ciit-meet-video-remote";
        tile.dataset.peerConnectionId = peerId;

        const video = document.createElement("video");
        video.autoplay = true;
        video.playsInline = true;
        video.setAttribute("aria-label", "Meeting participant video");

        const placeholder = document.createElement("div");
        placeholder.className = "ciit-meet-video-placeholder";
        const avatar = document.createElement("span");
        avatar.className = "ciit-meet-avatar ciit-meet-avatar-muted";
        avatar.textContent = "P";
        const label = document.createElement("strong");
        label.textContent = "Meeting participant";
        const detail = document.createElement("small");
        detail.textContent = "Camera is off";
        placeholder.append(avatar, label, detail);

        const name = document.createElement("span");
        name.className = "ciit-meet-video-name";
        name.textContent = "Participant";
        tile.append(video, placeholder, name);
        grid.append(tile);
        if (waitingTile) waitingTile.hidden = true;
        return { tile, video, placeholder };
    };

    const removePeer = peerId => {
        const peer = peers.get(peerId);
        if (!peer) return;
        peer.connection.ontrack = null;
        peer.connection.onicecandidate = null;
        peer.connection.onnegotiationneeded = null;
        peer.connection.close();
        peer.tile.tile.remove();
        peers.delete(peerId);
        if (grid.querySelectorAll(".ciit-meet-video-remote").length === 0 && waitingTile) {
            waitingTile.hidden = false;
        }
    };

    const sendSignal = (peerId, signalType, signalData) =>
        connection.invoke("RelayWebRtcSignal", sessionId, peerId, signalType, signalData);

    const createPeer = peerId => {
        if (!peerId || peerId === connection.connectionId) return null;
        if (peers.has(peerId)) return peers.get(peerId);

        const pc = new RTCPeerConnection({
            iceServers: []
        });
        const peer = {
            connection: pc,
            tile: createRemoteTile(peerId),
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
            peer.tile.video.srcObject = stream;
            peer.tile.video.hidden = false;
            peer.tile.placeholder.hidden = true;
            event.track.onmute = () => {
                peer.tile.placeholder.hidden = false;
            };
            event.track.onunmute = () => {
                peer.tile.placeholder.hidden = true;
            };
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

    const requestMedia = async kind => {
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
        stream.getTracks().forEach(track => {
            localStream.addTrack(track);
            peers.forEach(peer => peer.connection.addTrack(track, localStream));
        });
        updateLocalPreview();
    };

    const setTrackEnabled = async (kind, enabled) => {
        const mediaKind = kind === "mic" ? "audio" : "video";
        if (enabled && !mediaPermissions[kind]) {
            throw new Error("The host has disabled this device for you.");
        }
        if (enabled) {
            await requestMedia(mediaKind);
        }
        const track = localStream.getTracks().find(item => item.kind === mediaKind && item.readyState === "live");
        if (track) track.enabled = enabled;
        saveMediaPreference(kind, enabled);
        updateLocalPreview();
    };

    micButton?.addEventListener("click", async () => {
        const enabled = micButton.getAttribute("aria-pressed") === "true";
        try {
            await setTrackEnabled("mic", enabled);
        } catch (error) {
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
            const cameraTrack = localStream.getVideoTracks().find(track => track.readyState === "live");
            peers.forEach(peer => {
                const sender = peer.connection.getSenders().find(item => item.track?.kind === "video");
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
            if (!navigator.mediaDevices?.getDisplayMedia) throw new Error("Screen sharing is not supported in this browser.");
            screenStream = await navigator.mediaDevices.getDisplayMedia({ video: true, audio: false });
            const screenTrack = screenStream.getVideoTracks()[0];
            peers.forEach(peer => {
                const sender = peer.connection.getSenders().find(item => item.track?.kind === "video");
                if (sender) sender.replaceTrack(screenTrack).catch(error => console.error(error));
                else peer.connection.addTrack(screenTrack, screenStream);
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
                const cameraTrack = localStream.getVideoTracks().find(track => track.readyState === "live");
                peers.forEach(peer => {
                    const sender = peer.connection.getSenders().find(item => item.track?.kind === "video");
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
                const track = localStream.getTracks().find(item => item.kind === mediaKind);
                if (track) track.enabled = false;
                setToggleVisual(button, type, false);
                saveMediaPreference(type, false);
                updateLocalPreview();
            }

            if (!mediaPermissions.share && screenStream) {
                screenStream.getTracks().forEach(track => track.stop());
                screenStream = null;
                setToggleVisual(shareButton, "share", false);
                const cameraTrack = localStream.getVideoTracks().find(track => track.readyState === "live");
                peers.forEach(peer => {
                    const sender = peer.connection.getSenders().find(item => item.track?.kind === "video");
                    if (sender) sender.replaceTrack(cameraTrack || null).catch(error => console.error(error));
                });
                updateLocalPreview();
            }
        });
    };

    connection.on("MeetExistingPeers", peerIds => {
        (peerIds || []).forEach(peerId => {
            const peer = createPeer(peerId);
            if (peer) {
                addLocalTracksToPeer(peer);
                if (localStream.getTracks().length === 0) {
                    peer.connection.createDataChannel("ciit-meet-peer-init");
                }
            }
        });
    });
    connection.on("MeetPeerJoined", peerId => createPeer(peerId));
    connection.on("MeetPeerLeft", removePeer);
    connection.on("MeetWebRtcSignal", handleSignal);

    connection.onreconnected(() => {
        peers.forEach((peer, peerId) => removePeer(peerId));
    });

    window.ciitMeetWebRtcCleanup = () => {
        screenStream?.getTracks().forEach(track => track.stop());
        localStream.getTracks().forEach(track => track.stop());
        peers.forEach((peer, peerId) => removePeer(peerId));
        connection.invoke("LeavePeerRoom", sessionId).catch(() => {});
    };
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