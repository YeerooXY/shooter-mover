# Item Maker

Open `index.html` directly in a modern browser. No server, package manager, or build step is required.

The page creates gun and gear packages with exactly MK1, MK2, and MK3. It can import its own JSON packages for later editing and exports one `<item-id>.item.json` file.

The layout supports desktop, tablet, and mobile screens. On phones it switches to one column, uses touch-sized controls, keeps the MK tabs accessible, and places previews, calculations, checks, and JSON below the editor.

Current boundary:

- edits and validates item values;
- previews locally selected images without embedding them in the package;
- exports stable asset-reference text fields;
- does not install packages into Unity;
- does not replace the current live gun catalogue;
- does not implement charge or special gameplay.

Unfinished marks can still be exported by leaving `Available` off. Available marks must pass the page's gameplay and required-art checks.
