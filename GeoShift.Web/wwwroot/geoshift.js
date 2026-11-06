
window.geoShiftDownloadFile = async (fileName, streamReference) => {
    const arrayBuffer = await streamReference.arrayBuffer();
    const blob = new Blob([arrayBuffer], {
        type: "text/csv;charset=utf-8"
    });

    const url = URL.createObjectURL(blob);
    const anchor = document.createElement("a");

    anchor.href = url;
    anchor.download = fileName;

    document.body.appendChild(anchor);
    anchor.click();
    anchor.remove();

    URL.revokeObjectURL(url);
};

window.geoShiftSetupDropzone = (id) => {
    const zone = document.getElementById(id);
    if (!zone || zone.dataset.dropReady === "true") return;

    zone.dataset.dropReady = "true";
    let dragDepth = 0;

    const getInput = () => zone.querySelector('input[type="file"]');
    const openPicker = () => {
        const input = getInput();
        if (input && !input.disabled) input.click();
    };

    zone.addEventListener("click", (event) => {
        if (event.target.closest('input[type="file"]')) return;
        openPicker();
    });

    zone.addEventListener("keydown", (event) => {
        if (event.target !== zone || !["Enter", " "].includes(event.key)) return;
        event.preventDefault();
        openPicker();
    });

    zone.addEventListener("dragenter", (event) => {
        if (!event.dataTransfer?.types.includes("Files")) return;
        event.preventDefault();
        dragDepth++;
        zone.classList.add("gs-dropzone-active");
    });

    zone.addEventListener("dragover", (event) => {
        if (!event.dataTransfer?.types.includes("Files")) return;
        event.preventDefault();
        event.dataTransfer.dropEffect = "copy";
    });

    zone.addEventListener("dragleave", (event) => {
        event.preventDefault();
        dragDepth = Math.max(0, dragDepth - 1);
        if (dragDepth === 0) zone.classList.remove("gs-dropzone-active");
    });

    zone.addEventListener("drop", (event) => {
        event.preventDefault();
        dragDepth = 0;
        zone.classList.remove("gs-dropzone-active");

        const input = getInput();
        if (!input || input.disabled) return;

        const files = event.dataTransfer?.files;
        const migration = id === "gs-migration-dropzone";
        const maxFiles = migration ? 3 : 1;
        const extensions = migration ? [".csv", ".xlsx"] : [".kml", ".kmz"];
        if (!files || files.length < 1 || files.length > maxFiles) {
            window.alert(migration ? "Please drop up to three CSV or Excel files." : "Please drop one KML or KMZ file at a time.");
            return;
        }
        if (Array.from(files).some(file => !extensions.some(ext => file.name.toLowerCase().endsWith(ext)))) {
            window.alert(migration ? "Please select CSV or XLSX files." : "Please select a KML or KMZ file.");
            return;
        }
        const transfer = new DataTransfer();
        for (const file of files) transfer.items.add(file);
        input.files = transfer.files;
        input.dispatchEvent(new Event("change", { bubbles: true }));
    });
};

window.geoShiftMappingTemplates = {
    load: () => localStorage.getItem("geoshift.mappingTemplates.v1") || "[]",
    save: (json) => localStorage.setItem("geoshift.mappingTemplates.v1", json)
};
