// Set these exactly once on page load, so they are consistent for a single session, but still random across multiple sessions
let BuildGuId = crypto.randomUUID();
let SessionGuId = crypto.randomUUID()

// These values match an enum for RunMode in the HTML file
const RUN_MODE_WIN_PROCESS = "WinProcess";
const RUN_MODE_WIN_CONTAINER = "WinContainer";
const RUN_MODE_LINUX_CONTAINER = "LinuxContainer";

// regex strings for various validation steps
const ANTIREQUIRED_ABSOLUTE_PATH_SEARCH = ":"; // Rough attempt at identifying an apparent absolute path
const FAKE_PATH_SEARCH = "fakepath"; // New browsers spoof a fake path into JavaScript, and hide the real path
const SUGGESTED_WIN_EXTRACT_PATH_SEARCH = "C:\\\\Assets"; // Windows Process and Container modes are both suggested to use this path
const SUGGESTED_LINUX_EXTRACT_PATH_SEARCH = "/Data/"; // Linux container mode is suggested to use this root path
const REQUIRED_WIN_CONTAINER_EXTRACT_PATH_SEARCH = "C:\\\\"; // Windows Container always has exactly 1 drive, the C:\ drive

// User visible messages - Basically a string table for eventual translation if we go that far
const MSG_START_RELATIVE_PATH = "The Start Command should be a relative path into the zip file, not an absolute path.";
const MSG_START_ABSOLUTE_PATH = "The Start Command should be an absolute path that starts with the Asset Mount Path";
const MSG_START_EMPTY_PATH = "The Start Command should be empty (The container should launch the GSDK and Game Server directly)";
const MSG_OBSCURED_PATH = "Warning: This browser obscures the actual path of files. You will need to manually fix the LocalFilePath in the json";
const MSG_EXTRACT_WIN_PROCESS = "It is recommended that you choose C:\\Assets or a sub-folder";
const MSG_EXTRACT_WIN_CONTAINER = "Your path must start with the C:\\ drive for Windows containers";
const MSG_EXTRACT_LINUX_CONTAINER = "It is recommended that you choose a sub-folder of /Data";

function readWriteValue(value, valueName, lmaConfig){
    if(lmaConfig){
        lmaConfig[valueName] = value;
    }
    let mirrorElement = document.getElementById(valueName + "Output");
    if(mirrorElement){
        mirrorElement.innerHTML = value;
    }
}

function RunAllValidations(){
    ValidateStartCommand();
    ValidateAssetZip();
    ValidateMountPath();
}

function ValidateStartCommand(){
    let runMode = document.getElementById("RunMode").value;
    let startCommand = document.getElementById("StartCommand").value;

    let validationMessage = "";
    if (runMode == RUN_MODE_WIN_PROCESS){
        let isValid = (startCommand.search(ANTIREQUIRED_ABSOLUTE_PATH_SEARCH) == -1);
        if (!isValid){
            validationMessage = MSG_START_RELATIVE_PATH;
        }
    }else if(runMode == RUN_MODE_WIN_CONTAINER){
        let mountPath = document.getElementById("MountPath").value;
        // Verify that the mountPath is at index zero, and thus startCommand starts with mountPath
        let isValid = (startCommand.search(mountPath) == 0);
        if (!isValid){
            validationMessage = MSG_START_ABSOLUTE_PATH;
        }
    }else if(runMode == RUN_MODE_LINUX_CONTAINER){
        let mountPath = document.getElementById("MountPath").value;
        if (startCommand){
            validationMessage = MSG_START_EMPTY_PATH;
        }
    }

    // TODO: This could instead be a little red exclamation mark, with the validationMessage as hovertext
    document.getElementById("StartCommandValidate").innerHTML = validationMessage;
}

function ValidateAssetZip(){
    let validationMessage = "";
    let fakepathIndex = document.getElementById("LocalFilePath").value.search(FAKE_PATH_SEARCH);
    if (fakepathIndex != -1){
        validationMessage = MSG_OBSCURED_PATH;
    }

    document.getElementById("LocalFilePathValidate").innerHTML = validationMessage;
}

function ValidateMountPath(){
    let validationMessage = "";
    let warningMessage = "";
    let runMode = document.getElementById("RunMode").value;
    let mountPath = document.getElementById("MountPath").value;

    if (runMode == RUN_MODE_WIN_PROCESS){
        if(mountPath.search(SUGGESTED_WIN_EXTRACT_PATH_SEARCH) != 0){
            warningMessage = MSG_EXTRACT_WIN_PROCESS;
        }
    }else if(runMode == RUN_MODE_WIN_CONTAINER){
        if(mountPath.search(REQUIRED_WIN_CONTAINER_EXTRACT_PATH_SEARCH) != 0){
            validationMessage = MSG_EXTRACT_WIN_CONTAINER;
        } else if(mountPath.search(SUGGESTED_WIN_EXTRACT_PATH_SEARCH) != 0){
            warningMessage = MSG_EXTRACT_WIN_PROCESS;
        }
    }else if(runMode == RUN_MODE_LINUX_CONTAINER){
        if(mountPath.search(SUGGESTED_LINUX_EXTRACT_PATH_SEARCH) != 0){
            warningMessage = MSG_EXTRACT_LINUX_CONTAINER;
        }
    }

    // TODO: This could instead be a little red exclamation mark, with the validationMessage as hovertext
    document.getElementById("MountPathValidate").innerHTML = validationMessage;
    // TODO: This could instead be a little yellow hazard mark, with the warningMessage as hovertext
    document.getElementById("MountPathWarning").innerHTML = warningMessage;
}

function onInputChange(){
    let lmaConfig = {
        // Empty containers that will hold stuff that must exist
        "AssetDetails": [{}],
        "PortMappingsList": [[{"GamePort": {}}]]
    };

    if(document.getElementById("OutputPlaceholders").checked){
        lmaConfig.GameCertificateDetails = [];
        lmaConfig.SessionConfig = { "SessionCookie": null, };
        lmaConfig.DeploymentMetadata = { "Environment": "LOCAL", "FeaturesEnabled": "List,Of,Features,Enabled" };
        let initialPlayersArray = document.getElementById("InitialPlayers").value.split(',');
        for (let i = 0; i < initialPlayersArray.length; i++){
            initialPlayersArray[i]=initialPlayersArray[i].trim();
        }

        readWriteValue(SessionGuId, "SessionId", lmaConfig.SessionConfig);
        readWriteValue(initialPlayersArray, "InitialPlayers", lmaConfig.SessionConfig);
        readWriteValue(document.getElementById("TitleId").value, "TitleId", lmaConfig);
        readWriteValue(document.getElementById("Region").value, "Region", lmaConfig);
        readWriteValue(BuildGuId, "BuildId", lmaConfig);
    }

    let startCommand = document.getElementById("StartCommand").value;
    let runMode = document.getElementById("RunMode").value;

    readWriteValue(runMode != RUN_MODE_WIN_PROCESS, "RunMode", lmaConfig);
    if(runMode == RUN_MODE_WIN_PROCESS)
    {
        lmaConfig.ProcessStartParameters = {"StartGameCommand": startCommand};
    }else{
        lmaConfig.ContainerStartParameters =
        {
            "StartGameCommand": startCommand,
            "resourcelimits": { "cpus": 1, "memorygib": 16 },
            "imagedetails": { "registry": "mcr.microsoft.com", "imagename": "playfab/multiplayer", "imagetag": "wsc-10.0.17763.973.1", "username": "", "password": "" }
        };
        readWriteValue(document.getElementById("MountPath").value, "MountPath", lmaConfig.AssetDetails[0]);
        readWriteValue(document.getElementById("GamePortNumber").value, "Number", lmaConfig.PortMappingsList[0][0].GamePort);
    }


    readWriteValue(document.getElementById("OutputFolder").value, "OutputFolder", lmaConfig);
    readWriteValue(document.getElementById("LocalFilePath").value, "LocalFilePath", lmaConfig.AssetDetails[0]);

    readWriteValue(document.getElementById("NumHeartBeatsForActivateResponse").value, "NumHeartBeatsForActivateResponse", lmaConfig);
    readWriteValue(document.getElementById("NumHeartBeatsForTerminateResponse").value, "NumHeartBeatsForTerminateResponse", lmaConfig);

    readWriteValue(document.getElementById("AgentListeningPort").value, "AgentListeningPort", lmaConfig);
    readWriteValue(document.getElementById("NodePort").value, "NodePort", lmaConfig.PortMappingsList[0][0]);
    readWriteValue(document.getElementById("GamePortName").value, "Name", lmaConfig.PortMappingsList[0][0].GamePort);
    readWriteValue(document.getElementById("GamePortProtocol").value, "Protocol", lmaConfig.PortMappingsList[0][0].GamePort);

    document.getElementById("outputText").value = JSON.stringify(lmaConfig, null, 2);

    RunAllValidations();
}