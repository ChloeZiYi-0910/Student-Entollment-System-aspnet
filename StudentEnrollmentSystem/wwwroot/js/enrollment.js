document.addEventListener("DOMContentLoaded", function () {
    var countdown = document.getElementById("countdown");
    var seconds = 3;
    var interval = setInterval(function () {
        countdown.textContent = seconds;
        if (seconds === 0) {
            clearInterval(interval);
            window.location.href = '/Home'; // Change the URL to where you want to redirect.
        }
        seconds--;
    }, 1000);

    const searchInput = document.getElementById("searchCourse");
    const filterDay = document.getElementById("filterDay");
    const timeRangeStart = document.getElementById("timeRangeStart");
    const timeRangeEnd = document.getElementById("timeRangeEnd");
    const startTimeLabel = document.getElementById("startTimeLabel");
    const endTimeLabel = document.getElementById("endTimeLabel");
    const rows = document.querySelectorAll(".course-row");

    // Event listeners for dynamic filtering
    searchInput.addEventListener("input", filterCourses);
    filterDay.addEventListener("change", filterCourses);
    timeRangeStart.addEventListener("input", function () {
        startTimeLabel.textContent = formatTime(timeRangeStart.value);
        filterCourses();
    });
    timeRangeEnd.addEventListener("input", function () {
        endTimeLabel.textContent = formatTime(timeRangeEnd.value);
        filterCourses();
    });

    function formatTime(hour) {
        return `${hour.padStart(2, '0')}:00`;
    }

    function filterCourses() {
        let searchText = searchInput.value.toLowerCase();
        let selectedDay = filterDay.value.toLowerCase();
        let startTime = parseInt(timeRangeStart.value, 10);
        let endTime = parseInt(timeRangeEnd.value, 10);

        rows.forEach(row => {
            let courseID = row.getAttribute("data-course-id")?.toLowerCase() || "";
            let courseName = row.querySelector("td:nth-child(2)").innerText.toLowerCase();
            let day = row.querySelector("td:nth-child(3)").innerText.toLowerCase();
            let [startCourseTime, endCourseTime] = row.querySelector("td:nth-child(4)").innerText.split(" - ");

            let startCourseTimeNum = convertTimeToNum(startCourseTime);
            let endCourseTimeNum = convertTimeToNum(endCourseTime);

            let matchesSearch = courseID.includes(searchText) || courseName.includes(searchText);
            let matchesDay = selectedDay === "" || day === selectedDay;
            let matchesTime = (startCourseTimeNum >= startTime && startCourseTimeNum < endTime);

            row.style.display = (matchesSearch && matchesDay && matchesTime) ? "" : "none";
        });
    }

    function convertTimeToNum(time) {
        const [hours, minutes] = time.split(":").map(num => parseInt(num, 10));
        return hours + (minutes / 60);
    }

    // Clear Filters functionality
    document.getElementById("clearFilters").addEventListener("click", function () {
        searchInput.value = "";
        filterDay.value = "";
        timeRangeStart.value = 8;
        timeRangeEnd.value = 18;
        startTimeLabel.textContent = "08:00";
        endTimeLabel.textContent = "18:00";
        filterCourses(); // Reapply filters (now with all options visible)
    });

    // Initialize filters on page load
    filterCourses();
});


// Credit hours update function (as is)
function updateCreditHours() {
    let total = 0; // Reset total before recalculating
    let checkboxes = document.querySelectorAll(".select-course");

    checkboxes.forEach(checkbox => {
        if (checkbox.checked) {
            total += parseInt(checkbox.getAttribute("data-credits")); // Get credit hours
        }
    });

    // Update displayed total credit hours
    document.getElementById("totalCredit").innerText = total;
}

$(document).ready(function () {
    // When a table row is clicked, toggle the checkbox and update credits
    $('.course-row').on('click', function () {
        var checkbox = $(this).find('input[type="checkbox"]');

        if (!checkbox.prop('disabled')) {
            checkbox.prop('checked', !checkbox.prop('checked'));  // Toggle checked state
            updateCreditHours();  // Update credits dynamically
        }
    });

    // Ensure checkboxes trigger updateCreditHours() on change
    $('.select-course').on('change', function () {
        updateCreditHours();
    });
});