"use strict";

var connection = new signalR.HubConnectionBuilder().withUrl("/signalr").build();

connection.on("ReceiveControlLoopNotification", function (report) { 
    $("ul#activityList").append('<li>'+report+'</li>');
    var activities = $('ul#activityList li');
    if (activities.length > 10)
    {
        $('ul#activityList li:first-of-type').remove();
    }
});

connection.start();