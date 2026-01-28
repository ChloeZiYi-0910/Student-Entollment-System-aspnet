//Timetable Matching JS
//For Timetable Hover
document.addEventListener("DOMContentLoaded", function () {
    const table = document.querySelector(".table");
    const headers = table.querySelectorAll("thead th");

    headers.forEach((th, colIndex) => {
        th.addEventListener("mouseenter", () => highlightColumn(colIndex, true));
        th.addEventListener("mouseleave", () => highlightColumn(colIndex, false));
    });

    function highlightColumn(index, highlight) {
        const rows = table.querySelectorAll("tbody tr");

        rows.forEach(row => {
            let currentColIndex = 0; // Track actual column position
            row.querySelectorAll("td, th").forEach(cell => {
                const colspan = parseInt(cell.getAttribute("colspan")) || 1;

                // If index falls within the colspan range, highlight
                if (currentColIndex <= index && index < currentColIndex + colspan) {
                    cell.style.backgroundColor = highlight ? "#f1f1f1" : "";
                }

                currentColIndex += colspan; // Move index forward
            });
        });
    }

    var popoverTriggerList = [].slice.call(document.querySelectorAll('[data-bs-toggle="popover"]'));
    var popoverList = popoverTriggerList.map(function (popoverTriggerEl) {
        return new bootstrap.Popover(popoverTriggerEl);
    });
});

function printTimetable() {
    // Show the hidden print section
    const printSection = document.getElementById("printSection");
    printSection.style.display = "block";

    // Trigger print dialog
    window.print();

    // Hide the print section again
    printSection.style.display = "none";
}

//Timetable Matching JS End
//Contact Us JS Start
let filesArray = []; // Stores selected files
const allowedExtensions = [".jpg", ".jpeg", ".png", ".pdf"];
const maxFileSize = 5 * 1024 * 1024; // 5MB
const fileInput = document.getElementById("fileInput");
const submitBtn = document.getElementById("submitBtn");

if (fileInput) {
    fileInput.addEventListener("change", function (event) {
        let fileError = document.getElementById("fileError");
        fileError.textContent = ""; // Clear previous error

        let files = event.target.files; // Get all selected files
        if (!files.length) return; // No files selected

        for (let i = 0; i < files.length; i++) {
            let file = files[i];
            let fileExtension = file.name.split('.').pop().toLowerCase();

            if (!allowedExtensions.includes("." + fileExtension)) {
                fileError.textContent = "Invalid file type! Only JPG, PNG, and PDF are allowed.";
                continue;
            }

            if (file.size > maxFileSize) {
                fileError.textContent = "File size exceeds the 5MB limit!";
                continue;
            }

            filesArray.push(file);
        }

        updateFileList();
        event.target.value = ""; // Reset input so the same file can be selected again
    });
}
function updateFileList() {
    let fileList = document.getElementById("fileList");
    fileList.innerHTML = ""; // Clear previous list

    filesArray.forEach((file, index) => {
        let listItem = document.createElement("li");
        listItem.className = "list-group-item d-flex justify-content-between align-items-center";

        // File Name (Clickable for Preview)
        let fileLink = document.createElement("a");
        fileLink.href = "#";
        fileLink.innerText = file.name;
        fileLink.dataset.index = index;
        fileLink.setAttribute("data-bs-toggle", "modal");
        fileLink.setAttribute("data-bs-target", "#filePreviewModal");
        fileLink.onclick = function () { previewFile(file); };

        // Remove Button (X)
        let removeBtn = document.createElement("button");
        removeBtn.innerHTML = "&times;";
        removeBtn.className = "btn btn-danger btn-sm";
        removeBtn.onclick = function () { removeFile(index); };

        listItem.appendChild(fileLink);
        listItem.appendChild(removeBtn);
        fileList.appendChild(listItem);
    });
}
function previewFile(file) {
    let imagePreview = document.getElementById("imagePreview");
    let fileMessage = document.getElementById("fileMessage");

    if (file.type.startsWith("image/")) {
        let reader = new FileReader();
        reader.onload = function (e) {
            imagePreview.src = e.target.result;
            imagePreview.style.display = "block";
            fileMessage.style.display = "none";
        };
        reader.readAsDataURL(file);
    } else {
        imagePreview.style.display = "none";
        fileMessage.innerText = "File preview is not available.";
        fileMessage.style.display = "block";
    }
}
function removeFile(index) {
    filesArray.splice(index, 1); // Remove from array
    updateFileList(); // Refresh list
}

if (submitBtn) {
    submitBtn.addEventListener("click", function (event) {
        event.preventDefault(); // Prevent normal form submission

        let form = document.querySelector("form");
        let isValid = true; // Flag to track validation

        // Reset previous error messages
        document.querySelectorAll(".text-danger").forEach(span => span.textContent = "");

        // Validate Category
        let category = document.querySelector("select[name='Enquiry.Category']");
        if (!category.value) {
            category.nextElementSibling.textContent = "Please select the category related";
            isValid = false;
        }

        // Validate Subject
        let subject = document.querySelector("input[name='Enquiry.Subject']");
        if (!subject.value.trim()) {
            subject.nextElementSibling.textContent = "Please fill the Subject related to enquiry";
            isValid = false;
        }

        // Validate Message
        let message = document.querySelector("textarea[name='Enquiry.Message']");
        if (!message.value.trim()) {
            message.nextElementSibling.textContent = "Please provide some description on the enquiry";
            isValid = false;
        }
        if (!isValid) return; // Stop submission if validation fails

        let formData = new FormData(form);


        // Only append files if there are any
        if (filesArray.length > 0) {
            filesArray.forEach((file) => {
                formData.append("UploadedFiles", file);
            });
        }

        fetch(form.action, {
            method: "POST",
            body: formData,
        })
            .then(response => {
                if (response.redirected) {
                    // If server redirects, follow the redirection
                    window.location.href = response.url;
                } else {
                    return response.text();
                }
            })
            .then(data => {
                console.log("Success:", data);
            })
            .catch(error => {
                console.error("Error:", error);
            });
    });
}