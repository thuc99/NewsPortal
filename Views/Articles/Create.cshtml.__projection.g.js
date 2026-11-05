/* BEGIN EXTERNAL SOURCE */

    document.getElementById("imageFile").addEventListener("change", function (event) {
        var file = event.target.files[0];
        var preview = document.getElementById("previewImage");

        if (file && file.type.startsWith("image/")) {
            preview.src = URL.createObjectURL(file);
            preview.style.display = "block";
        } else {
            preview.removeAttribute("src");
            preview.style.display = "none";
        }
    });

/* END EXTERNAL SOURCE */
