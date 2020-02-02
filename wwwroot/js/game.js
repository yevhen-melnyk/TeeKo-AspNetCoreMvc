
//global counters
var drawingsSent = 0;
var linesSent = 0;

//Max sendings per player
var maxLines = 0;
var maxDrawings = 0;

//This is being set outside this script via razor processor from model
//var gameId

//Phase change functions
function PhaseChange(phase, phaseDuration) {


    var timeLeft = phaseDuration;
    setInterval(function () {
        if (timeLeft >= 0) {
            timeLeft -= 1;
            $("#timer").text(timeLeft + " seconds");
        }
    }, 1000);

    $("#gameStage").text(phase);

    if (phase == "drawing") {
        StartDrawing();
    }

    if (phase == "writing") {
        StartWriting();

    }
    if (phase == "composing") {
        StartComposing();
    }
    if (phase == "voting") {
        StartVoting();
    }

    if (phase == "waiting") {
        StartWaiting();
    }



}
//Globally accessible variable that will hold LC controller
var lc;

function StartDrawing() {


    $('#lobby').hide();
    $('#composingPhase').hide();
    $('#drawingPhase').show();
    $('#drawingPhase').css("opacity", "100%");
    $('#writingPhase').hide();
    $('#votingPhase').hide();


    linesSent = 0;
    $("#linesCount").text(linesSent + '/' + maxLines);
    $("#drawingCount").text(drawingsSent + '/' + maxDrawings);

    //start lc
    //Note: it is crucial to init lc only on elements that AREN'T 0x0 size
    lc = LC.init(
        document.getElementsByClassName('my-drawing')[0],
        {
            imageURLPrefix: '/images/literallycanvas',
            imageSize: { width: 600, height: 400 },
            tools:
                [
                    LC.tools.Pencil,
                    LC.tools.Eraser,
                    LC.tools.Line,
                    LC.tools.Rectangle,
                    LC.tools.Polygon,
                    LC.tools.Pan,
                    LC.tools.Eyedropper
                ]
        }
    );

}

function StartWriting() {

    //Try submitting in case someone didn't submit in time
    $('#submitDrawing').click();

    $('#lobby').hide();
    $('#composingPhase').hide();
    $('#drawingPhase').hide();
    $('#writingPhase').show();
    $('#votingPhase').hide();
}

function StartComposing() {
    $("#composing-drawings").empty();
    $("#composing-lines").empty();

    $('#lobby').hide();
    $('#composingPhase').show();
    $('#drawingPhase').hide();
    $('#writingPhase').hide();
    $('#votingPhase').hide();

    hubConnection.invoke("RequestComposingElements", gameId);

}

function StartVoting() {

    selectedImage = "";
    selectedLine = "";

    $("#compositionsHolder").empty();

    $('#lobby').hide();
    $('#composingPhase').hide();
    $('#drawingPhase').hide();
    $('#writingPhase').hide();
    $('#votingPhase').show();

    hubConnection.invoke("RequestVotingCandidates", gameId);
}

function StartWaiting() {

    drawingsSent = 0;

    selectedImage = "";
    selectedLine = "";

    $("#timer").text("until game host is ready");
    $('#lobby').show();
    $('#drawingPhase').hide();
    $('#writingPhase').hide();
    $('#votingPhase').hide();
}

//Globally accissible hubConnection
var hubConnection;

//Setup
$(function () {
    hubConnection = new signalR.HubConnectionBuilder()
        .withUrl("/game", {
            skipNegotiation: true,
            transport: signalR.HttpTransportType.WebSockets
        })
        .build();
    var canvas = $('.my-drawing');

    //Recieving
    hubConnection.on("PhaseChange", function (phase, phaseDuration) { PhaseChange(phase, phaseDuration) });

    //Basic info
    hubConnection.on("BasicInfo", function (maxD, maxL) {
        maxDrawings = maxD;
        maxLines = maxL;
    });

    //Feedback from server — server recieved a drawing
    hubConnection.on("DrawingRecieved", function () {
        drawingsSent += 1;
        $("#drawingCount").text(drawingsSent + '/' + maxDrawings);
    });

    //Feedback from server — server recieved the last allowed drawing
    hubConnection.on("LastDrawingRecieved", function () {

        $("#gameStage").text($("#gameStage").text() + " waiting for other players");
        $('#drawingPhase').hide();
    });

    //Feedback from server — server recieved a line
    hubConnection.on("LineRecieved", function () {
        linesSent += 1;
        $("#linesCount").text(linesSent + '/' + maxLines);
    });

    //Feedback from server — server recieved the last allowed line
    hubConnection.on("LastLineRecieved", function () {
        $("#gameStage").text($("#gameStage").text() + " waiting for other players");
        $('#writingPhase').hide();

    });

    //Feedback from server — server recieved the composition
    hubConnection.on("CompositionRecieved", function () {
        $("#gameStage").text($("#gameStage").text() + " waiting for other players");
        $('#composingPhase').hide();

    });

    //Feedback from server — server recieved the vote
    hubConnection.on("VoteRecieved", function () {
        $("#gameStage").text($("#gameStage").text() + " waiting for other players");
        $('#votingPhase').hide();

    });

    var selectedImage;

    var selectedLine;

    //Feedback from server — server sent the requested elements for composing phase
    hubConnection.on("ComposingElementsProvided", function (drawings, lines) {
        //append all drawings recieved with their authorid as values
        for (var i = 0; i < drawings.length; i++) {
            $('<img/>', {
                "class": 'composing-drawing',
                "src": drawings[i].imgDataUrl,
                "style": "width: 300px; height: 300px; border: 2px double black"



            }).appendTo('#composing-drawings');
        }
        // Set image behaviours
        $('.composing-drawing').click(function (e) {
            var element = $(e.target);
            selectedImage = element.attr('src');;
            $('.composing-drawing').css("border", "2px double black");
            element.css("border", "4px solid blue");
        });

        //same for lines
        for (var i = 0; i < lines.length; i++) {
            var line = $('<div/>', {
                "class": 'composing-line',
                "style": "font-size: large; border: 2px double black; width:100%"
            });
            line.text(lines[i].text);
            line.appendTo('#composing-lines');
            //$('<br/>').appendTo('#composing-lines');
        }

        // Set lines behaviours
        $('.composing-line').click(function (e) {
            var element = $(e.target);
            selectedLine = element.text();
            $('.composing-line').css("border", "2px double black");
            element.css("border", "4px solid blue");
        });
    });


    var defaultCandidateStyle = "border: 4px double black";

    //Feedback from server — server sent the requested elements for composing phase
    hubConnection.on("VotingCandidatesProvided", function (compositions) {
        //append all drawings recieved with their authorid as values
        for (var i = 0; i < compositions.length; i++) {
            var div = $('<div/>', {
                "class": 'voting-candidate',
                "style": defaultCandidateStyle
            });

            var img = $('<img/>', {
                "class": 'voting-drawing',
                "src": compositions[i].drawing.imgDataUrl,
                "style": "width: 400px; height: 400px;"
            });

            var text = $('<div/>', {
                "class": 'voting-text'
            });
            text.text(compositions[i].line.text);
            div.append(img);
            div.append(text);
            $("#compositionsHolder").append(div);

        }
        // Set candidate behaviours
        $(".voting-candidate").click(function (e) {
            var element = $(this);

            selectedImage = element.find(".voting-drawing").attr('src');
            selectedLine = element.find(".voting-text").text();
            $('.voting-drawing').css(defaultCandidateStyle);
            element.css("border", "4px solid blue");
        });



    });


    //When an update on player list arrives
    hubConnection.on("PlayersChanged", function (publicStats) {
        $("#player-count").text(publicStats.length);
        $("#player-list").empty()
        var stats = publicStats;
        for (var i = 0; i < stats.length; i++) {
            $("#player-list").append('<li>' + stats[i].name + " — " + stats[i].score + " points" + '</li>');
        }
    });




    //Sending drawings
    $('#submitDrawing').click(function () {
        if (drawingsSent >= maxDrawings) {
        if ($("#drcountalert").length <= 3) {
            var alert = $("<h4 class='alert-danger' id='drcountalert'></h4>").text("You already submited the maximum amout of drawings.");
            $("#drawingPhase").append(alert);
        }
    }
            else {
            hubConnection.invoke("submitDrawing", lc.getImage().toDataURL(), gameId);
        lc.clear();
    }
});

//when enter is pressed on line input field
$('#lineInput').keypress(function (event) {
    if (event.which == 13) {
        $('#submitLine').click();
    }
});

//Sending lines
$('#submitLine').click(function () {
    if (linesSent >= maxLines) {
    if ($("#lncountalert").length <= 3) {
        var alert = $("<h4 class='alert-danger' id='lncountalert'></h4>").text("You already submited the maximum amout of lines.");
        $("#writingPhase").append(alert);
    }
}
            else {
        hubConnection.invoke("submitLine", $("#lineInput").val(), gameId);
    lc.clear();
    $("#lineInput").val("");
}
       });

//Sending compositions
$('#submitComposition').click(function () {
    if (selectedImage.length == 0 || selectedLine.length == 0) {
        alert("Select an image and a line of text first!");
    }
    else {
        hubConnection.invoke("SubmitComposing",
            selectedImage, selectedLine,
            gameId);
    }
});

//Sending compositions
$('#submitVote').click(function () {
    if (selectedImage.length == 0 || selectedLine.length == 0) {
        alert("Select an image and a line of text first!");
    }
    else {
        hubConnection.invoke("SubmitVote",
            selectedImage, selectedLine,
            gameId);
    }
});

$('#startGame').click(function () {

    hubConnection.invoke("startGame", gameId);
});

//Start
hubConnection.start().then(function () {
    
    hubConnection.invoke("Connected", gameId);
});
hubConnection.serverTimeoutInMilliseconds = 600000;

        });