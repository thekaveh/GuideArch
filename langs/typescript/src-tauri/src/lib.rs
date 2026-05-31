// GuideArch's Tauri entry point. v1.0 ships a single browser-mode UX
// (see spec/editors.md §3 — file dialogs use FileReader + anchor-download
// in both browser and Tauri runs), so there are no custom IPC commands
// from the Rust side. The plugin-dialog / plugin-fs integration that
// would let Tauri use the OS-native picker is on the v1.1 backlog.

#[cfg_attr(mobile, tauri::mobile_entry_point)]
pub fn run() {
    tauri::Builder::default()
        .run(tauri::generate_context!())
        .expect("error while running tauri application");
}
