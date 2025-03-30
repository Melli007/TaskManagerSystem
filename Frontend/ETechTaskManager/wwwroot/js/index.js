
// Listen for task updates
taskConnection.on("ReceiveTask", (Id, title, description, status, createdAt, dueDate, createdByName,filePath) => {
    console.log("Task received:", { Id, title, description, status, createdAt, dueDate, createdByName ,filePath });

    // Update the task table in the UI dynamically
    updateTaskTable(Id, title, description, status, createdAt, dueDate, createdByName,filePath);
});

// Listen for task status updates
taskConnection.on("UpdateTaskStatus", (id, newStatus) => {
    console.log(`Task ${id} status changed to: ${newStatus}`);

    // Find the task row and update the status
    const taskRow = document.querySelector(`tr[data-task-id="${id}"]`);
    if (taskRow) {
        const statusCell = taskRow.querySelector("td:nth-child(4)"); // Assuming status is in the 4th column
        if (statusCell) {
            statusCell.textContent = newStatus;
        }
    }
});

// Listen for task updates (full update)
taskConnection.on("TaskUpdated", (id, title, description, status, dueDate, filePath) => {
    console.log(`Task ${id} updated: ${title}`);

    // Find the task row and update the task details
    const taskRow = document.querySelector(`tr[data-task-id="${id}"]`);
    if (taskRow) {
        taskRow.querySelector("td:nth-child(2)").textContent = title; // Update title
        taskRow.querySelector("td:nth-child(3)").textContent = description; // Update description
        taskRow.querySelector("td:nth-child(4)").textContent = status; // Update status
        taskRow.querySelector("td:nth-child(6)").textContent = new Date(dueDate).toLocaleString(); // Update due date
        taskRow.querySelector("td:nth-child(7)").textContent = filePath;
    }
});

// Listen for task deletions
taskConnection.on("TaskDeleted", (id) => {
    console.log(`Task ${id} deleted.`);

    // Remove the task row from the table
    const taskRow = document.querySelector(`tr[data-task-id="${id}"]`);
    if (taskRow) {
        taskRow.remove();
    }
});


taskConnection.on("ReceiveTaskUpdate", (totalTasks, completedTasks, inprogressTasks, pendingTasks, nodeadlineTasks) => {
    console.log("Task counts updated:", { totalTasks, completedTasks, inprogressTasks, pendingTasks, nodeadlineTasks });

    // Update the dashboard counts dynamically
    document.querySelector('[data-dashboard="num_task"]').textContent = `${totalTasks} Gjitha Tasket`;
    document.querySelector('[data-dashboard="completed"]').textContent = `${completedTasks} Te Përfunduara`;
    document.querySelector('[data-dashboard="in_progress"]').textContent = `${inprogressTasks} Ne Progres`;
    document.querySelector('[data-dashboard="pending"]').textContent = `${pendingTasks} Ne Pritje`;
    document.querySelector('[data-dashboard="nodeadline_task"]').textContent = `${nodeadlineTasks} Pa Deadline`;
});

// Reconnect logic: attempt to reconnect every 5 seconds
taskConnection.onclose(async () => {
    console.log("Connection lost. Reconnecting...");
    try {
        await taskConnection.start();
        console.log("Reconnected to TaskHub");
    } catch (err) {
        console.error("Reconnection failed: ", err.toString());
        setTimeout(() => taskConnection.start(), 5000); // Retry after 5 seconds
    }
});
// Function to update the task table in the UI
function updateTaskTable(Id, title, description, status, createdAt, dueDate, createdByName,filePath) {
    const taskTableBody = document.querySelector(".content-table tbody");
    if (!taskTableBody) return;

    // Check if a row for this task ID already exists
    let existingRow = Array.from(taskTableBody.querySelectorAll("tr")).find(row => {
        const taskIdCell = row.querySelector("td");
        return taskIdCell && taskIdCell.textContent.trim() === Id.toString();
    });

    // Determine file cell content purely on the client
    let fileCellContent = "";
    if (filePath && filePath.trim() !== "") {
        fileCellContent = `<a href="/TasksController1/DownloadFile?taskId=${Id}" class="btn btn-primary" title="Download File">
                               <svg fill="currentColor" height="24px" width="24px" version="1.1" id="Capa_1" class="download-icon" style="margin-left: 20;" xmlns="http://www.w3.org/2000/svg" xmlns:xlink="http://www.w3.org/1999/xlink"
                                 viewBox="0 0 29.978 29.978" xml:space="preserve">
                            <g>
                            <path d="M25.462,19.105v6.848H4.515v-6.848H0.489v8.861c0,1.111,0.9,2.012,2.016,2.012h24.967c1.115,0,2.016-0.9,2.016-2.012	v-8.861H25.462z" />
                            <path d="M14.62,18.426l-5.764-6.965c0,0-0.877-0.828,0.074-0.828s3.248,0,3.248,0s0-0.557,0-1.416c0-2.449,0-6.906,0-8.723
		c0,0-0.129-0.494,0.615-0.494c0.75,0,4.035,0,4.572,0c0.536,0,0.524,0.416,0.524,0.416c0,1.762,0,6.373,0,8.742
		c0,0.768,0,1.266,0,1.266s1.842,0,2.998,0c1.154,0,0.285,0.867,0.285,0.867s-4.904,6.51-5.588,7.193
		C15.092,18.979,14.62,18.426,14.62,18.426z" /><g></svg>
                                Download
                           </a>`;
    } else {
        fileCellContent = "<span>Nuk Ka File</span>";
    }

    if (existingRow) {
        // Update existing task row
        const cells = existingRow.querySelectorAll("td");
        cells[1].textContent = Id;
        cells[2].textContent = title;
        cells[3].textContent = description;
        cells[4].textContent = status;
        cells[5].textContent = new Date(createdAt).toLocaleString();
        cells[6].textContent = new Date(dueDate).toLocaleString();
        cells[7].textContent = createdByName;
        cells[8].textContent = filePath;
    } else {
        // Add a new row for the task
        const newRow = document.createElement("tr");
        newRow.dataset.taskId = Id; // Use data attribute to store task ID
        newRow.innerHTML = `
            <td>${Id}</td>
            <td>${title}</td>
            <td>${description}</td>
            <td>${status}</td>
            <td>${new Date(createdAt).toLocaleString()}</td>
            <td>${new Date(dueDate).toLocaleString()}</td>
            <td>${createdByName}</td>
            <td>${fileCellContent}</td>
                
            <td>
                <form asp-action="ChangeStatus" asp-route-id="${Id}" method="post">
                    <button type="submit" class="btn btn-primary dropbtn">Ndrysho Statusin</button>
                </form>
            </td>
        `;
        taskTableBody.appendChild(newRow);
    }
}
