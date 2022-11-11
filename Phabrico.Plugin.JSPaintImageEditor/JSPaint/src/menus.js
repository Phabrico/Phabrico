((exports) => {

	const looksLikeChrome = !!(window.chrome && (window.chrome.loadTimes || window.chrome.csi));
	// NOTE: Microsoft Edge includes window.chrome.app
	// (also this browser detection logic could likely use some more nuance)

	const menus = {
		[localize("&File")]: [
			{
				item: localize("&Save"),
				shortcut: "Ctrl+S",
				action: () => { file_save(); },
				description: localize("Saves the active document."),
			},
			MENU_DIVIDER,
			{
				item: localize("&Load From URL"),
				// shortcut: "", // no shortcut: Ctrl+L is taken, and you can paste a URL with Ctrl+V, so it's not really needed
				action: () => { file_load_from_url(); },
				description: localize("Opens an image from the web."),
			},
			MENU_DIVIDER,
			{
				item: localize("Manage Storage"),
				action: () => { manage_storage(); },
				description: localize("Manages storage of previously created or opened pictures."),
			},
			MENU_DIVIDER,
			{
				item: localize("E&xit"),
				action: () => {
					window.parent.postMessage({
						event: "exit"
					}, "*");
				},
				description: localize("Quits Paint."),
			}
		],
		[localize("&Edit")]: [
			{
				item: localize("&Undo"),
				shortcut: "Ctrl+Z",
				enabled: () => undos.length >= 1,
				action: () => { undo(); },
				description: localize("Undoes the last action."),
			},
			{
				item: localize("&Repeat"),
				shortcut: "F4", // also supported: Ctrl+Shift+Z, Ctrl+Y
				enabled: () => redos.length >= 1,
				action: () => { redo(); },
				description: localize("Redoes the previously undone action."),
			},
			{
				item: localize("&History"),
				shortcut: "Ctrl+Shift+Y",
				action: () => { show_document_history(); },
				description: localize("Shows the document history and lets you navigate to states not accessible with Undo or Repeat."),
			},
			MENU_DIVIDER,
			{
				item: localize("Cu&t"),
				shortcut: "Ctrl+X",
				enabled: () =>
					// @TODO: support cutting text with this menu item as well (e.g. for the text tool)
					!!selection,
				action: () => {
					edit_cut(true);
				},
				description: localize("Cuts the selection and puts it on the Clipboard."),
			},
			{
				item: localize("&Copy"),
				shortcut: "Ctrl+C",
				enabled: () =>
					// @TODO: support copying text with this menu item as well (e.g. for the text tool)
					!!selection,
				action: () => {
					edit_copy(true);
				},
				description: localize("Copies the selection and puts it on the Clipboard."),
			},
			{
				item: localize("&Paste"),
				//shortcut: "Ctrl+V",
				enabled: () =>
					// @TODO: disable if nothing in clipboard or wrong type (if we can access that)
					true,
				action: () => {
					edit_paste(true);
				},
				description: localize("Inserts the contents of the Clipboard."),
			},
			{
				item: localize("C&lear Selection"),
				shortcut: "Del",
				enabled: () => !!selection,
				action: () => { delete_selection(); },
				description: localize("Deletes the selection."),
			},
			{
				item: localize("Select &All"),
				shortcut: "Ctrl+A",
				action: () => { select_all(); },
				description: localize("Selects everything."),
			},
			MENU_DIVIDER,
			{
				item: `${localize("C&opy To")}...`,
				enabled: () => !!selection,
				action: () => { save_selection_to_file(); },
				description: localize("Copies the selection to a file."),
			},
			{
				item: `${localize("Paste &From")}...`,
				action: () => { choose_file_to_paste(); },
				description: localize("Pastes a file into the selection."),
			},
			MENU_DIVIDER,
			{
				item: "↕️ " + localize("&Vertical Color Box"),
				checkbox: {
					toggle: () => {
						if (location.hash.match(/vertical-color-box-mode/i)) {
							change_url_param("vertical-color-box-mode", false);
						} else {
							change_url_param("vertical-color-box-mode", true);
						}
					},
					check: () => {
						return location.hash.match(/vertical-color-box-mode/i);
					},
				},
				enabled: () => {
					return true;
				},
				description: localize("Arranges the color box vertically."),
			},
		],
		[localize("&View")]: [
			{
				item: localize("&Tool Box"),
				checkbox: {
					toggle: () => {
						$toolbox.toggle();
					},
					check: () => $toolbox.is(":visible"),
				},
				description: localize("Shows or hides the tool box."),
			},
			{
				item: localize("&Color Box"),
				shortcut: "Ctrl+L", // focuses browser address bar, but Firefox and Chrome both allow overriding the default behavior
				checkbox: {
					toggle: () => {
						$colorbox.toggle();
					},
					check: () => $colorbox.is(":visible"),
				},
				description: localize("Shows or hides the color box."),
			},
			{
				item: localize("&Status Bar"),
				checkbox: {
					toggle: () => {
						$status_area.toggle();
					},
					check: () => $status_area.is(":visible"),
				},
				description: localize("Shows or hides the status bar."),
			},
			{
				item: localize("T&ext Toolbar"),
				enabled: false, // @TODO: toggle fonts box
				checkbox: {},
				description: localize("Shows or hides the text toolbar."),
			},
			MENU_DIVIDER,
			{
				item: localize("&Zoom"),
				submenu: [
					{
						item: localize("&Normal Size"),
						description: localize("Zooms the picture to 100%."),
						enabled: () => magnification !== 1,
						action: () => {
							set_magnification(1);
						},
					},
					{
						item: localize("&Large Size"),
						description: localize("Zooms the picture to 400%."),
						enabled: () => magnification !== 4,
						action: () => {
							set_magnification(4);
						},
					},
					{
						item: localize("Zoom To &Window"),
						description: localize("Zooms the picture to fit within the view."),
						action: () => {
							const rect = $canvas_area[0].getBoundingClientRect();
							const margin = 30; // leave a margin so scrollbars won't appear
							let mag = Math.min(
								(rect.width - margin) / main_canvas.width,
								(rect.height - margin) / main_canvas.height,
							);
							// round to an integer percent for the View > Zoom > Custom... dialog, which shows non-integers as invalid
							mag = Math.floor(100 * mag) / 100;
							set_magnification(mag);
						},
					},
					{
						item: `${localize("C&ustom")}...`,
						description: localize("Zooms the picture."),
						action: () => { show_custom_zoom_window(); },
					},
					MENU_DIVIDER,
					{
						item: localize("Show &Grid"),
						shortcut: "Ctrl+G",
						enabled: () => magnification >= 4,
						checkbox: {
							toggle: () => { toggle_grid(); },
							check: () => show_grid,
						},
						description: localize("Shows or hides the grid."),
					},
					{
						item: localize("Show T&humbnail"),
						checkbox: {
							toggle: () => { toggle_thumbnail(); },
							check: () => show_thumbnail,
						},
						description: localize("Shows or hides the thumbnail view of the picture."),
					}
				]
			},
			{
				item: localize("&View Bitmap"),
				shortcut: "Ctrl+F",
				action: () => { view_bitmap(); },
				description: localize("Displays the entire picture."),
			},
			MENU_DIVIDER,
			{
				item: localize("&Fullscreen"),
				shortcut: "F11", // relies on browser's shortcut
				enabled: () => document.fullscreenEnabled || document.webkitFullscreenEnabled,
				checkbox: {
					check: () => document.fullscreenElement || document.webkitFullscreenElement,
					toggle: () => {
						if (document.fullscreenElement || document.webkitFullscreenElement) {
							if (document.exitFullscreen) { document.exitFullscreen(); }
							else if (document.webkitExitFullscreen) { document.webkitExitFullscreen(); }
						} else {
							if (document.documentElement.requestFullscreen) { document.documentElement.requestFullscreen(); }
							else if (document.documentElement.webkitRequestFullscreen) { document.documentElement.webkitRequestFullscreen(); }
						}
						// check() would need to be async or faked with a timeout,
						// if the menus stayed open. @TODO: make all checkboxes close menus
						menu_bar.closeMenus();
					},
				},
				description: localize("Makes the application take up the entire screen."),
			},
		],
		[localize("&Image")]: [
			{
				item: localize("&Flip/Rotate"),
				shortcut: "Ctrl+Alt+R",
				action: () => { image_flip_and_rotate(); },
				description: localize("Flips or rotates the picture or a selection."),
			},
			{
				item: localize("&Stretch/Skew"),
				shortcut: "Ctrl+Alt+W",
				action: () => { image_stretch_and_skew(); },
				description: localize("Stretches or skews the picture or a selection."),
			},
			{
				item: localize("&Invert Colors"),
				shortcut: "Ctrl+I",
				action: () => { image_invert_colors(); },
				description: localize("Inverts the colors of the picture or a selection."),
			},
			{
				item: `${localize("&Attributes")}...`,
				shortcut: "Ctrl+E",
				action: () => { image_attributes(); },
				description: localize("Changes the attributes of the picture."),
			},
			{
				item: localize("&Clear Image"),
				action: () => { !selection && clear(); },
				enabled: () => !selection,
				description: localize("Clears the picture."),
			},
			{
				item: localize("&Draw Opaque"),
				checkbox: {
					toggle: () => {
						tool_transparent_mode = !tool_transparent_mode;
						$G.trigger("option-changed");
					},
					check: () => !tool_transparent_mode,
				},
				description: localize("Makes the current selection either opaque or transparent."),
			}
		],
		[localize("&Colors")]: [
			{
				item: `${localize("&Edit Colors")}...`,
				action: () => {
					show_edit_colors_window();
				},
				description: localize("Creates a new color."),
			},
			{
				item: localize("&Get Colors"),
				action: async () => {
					const { file } = await systemHooks.showOpenFileDialog({ formats: palette_formats });
					AnyPalette.loadPalette(file, (error, new_palette) => {
						if (error) {
							show_file_format_errors({ as_palette_error: error });
						} else {
							palette = new_palette.map((color) => color.toString());
							$colorbox.rebuild_palette();
							window.console && console.log(`Loaded palette: ${palette.map(() => `%c█`).join("")}`, ...palette.map((color) => `color: ${color};`));
						}
					});
				},
				description: localize("Uses a previously saved palette of colors."),
			},
			{
				item: localize("&Save Colors"),
				action: () => {
					const ap = new AnyPalette.Palette();
					ap.name = "JS Paint Saved Colors";
					ap.numberOfColumns = 16; // 14?
					for (const color of palette) {
						const [r, g, b] = get_rgba_from_color(color);
						ap.push(new AnyPalette.Color({
							red: r / 255,
							green: g / 255,
							blue: b / 255,
						}));
					}
					systemHooks.showSaveFileDialog({
						dialogTitle: localize("Save Colors"),
						defaultFileName: localize("untitled.pal"),
						formats: palette_formats,
						getBlob: (format_id) => {
							const file_content = AnyPalette.writePalette(ap, AnyPalette.formats[format_id]);
							const blob = new Blob([file_content], { type: "text/plain" });
							return new Promise((resolve) => {
								sanity_check_blob(blob, () => {
									resolve(blob);
								});
							});
						},
					});
				},
				description: localize("Saves the current palette of colors to a file."),
			}
		],
		[localize("&Help")]: [
			{
				item: localize("&About Paint"),
				action: () => { show_about_paint(); },
				description: localize("Displays information about this application."),
				//description: localize("Displays program information, version number, and copyright."),
			}
		],
	};

	exports.menus = menus;

})(window);
