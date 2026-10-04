# KiWeave Stream Deck plugin

This folder contains the official-SDK-shaped Windows plugin bundle for KiWeave. Copy `com.kiweave.streamdeck.sdPlugin` into the Stream Deck plugins directory, then enable the Stream Deck first-party extension in KiWeave.

The plugin forwards only `status`, `show-settings`, and `activate-profile` messages over the per-user named pipe. It never executes arbitrary commands, stores secrets, or downloads code.
