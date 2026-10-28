// Initialization runs once, after all screen/component modules have registered.
GaidenUI.state.ready=true;
applyPrefs();
GaidenUI.commitSnapshot(window.gaidenBootstrap||GaidenUI.state.snapshot);
