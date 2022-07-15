# Cloud Assisted Gaming
Course Project for ShanghaiTech CS225 Advanced Distributed Systems

Authors: [wjw78879](https://github.com/wjw78879), [Zhang Yi-ning](https://github.com/yining765)

### Client

The client code is in `Client/`, it is a Unity project and could be opened with Unity 2021.3.0f1c1.

To build the client, open the client project with Unity and build. Then you need to copy all the `.dll` files in `Client/Assets/Plugins/FFmpeg/` to the root folder of the build and create a  `config.yml` based on `client_config.yml`, .

You can edit the config file, where the most important parameters are:
-   Width & Height: the resolution of the game
-   FrameRate: the frame rate of the game
-   BufferSize: the client window size, should be a fraction (about 1/3 - 1/2) of the FrameRate value.
-   SatisfyingDelayMilliseconds: determines the final experienced gameplay delay, if the network status is good.
-   ScheduleServerHost: the IP address and port of the scheduler.
-   TargetServerCount: the number of servers you want.
-   StatsPeriod: the period of logging.

Then, run the build and the client will start to find servers from the scheduler server.

###### Game Instructions

WASD: move

hold right mouse button and move mouse: view

Space: shoot random object

### Server

The server is in `Server/`, it is a Unity project and could be opened with Unity 2021.3.0f1c1.

To build the client, open the client project with Unity and build. Then you need to copy all the `.dll` files in `Server/Assets/Plugins/FFmpeg/` to the build directory, and create a `config.yml` based on `server_config.yml`, .

You can edit the config file, where the most important parameters are:
-   ScheduleListenPort: the port on which the server listens the RPC calls from the scheduler.
-   EncodePreset: the encode preset of ffmpeg. Should be one of: {UltraFast, SuperFast, VeryFast, Faster, Fast, Medium, Slow, Slower, VerySlow, Placebo}.
-   Crf: the Constant Rate Factor for encoding. Should be an integer value from 0 to 51. Smaller value means better quality and larger size. (Should not be too small)
-   Gop: the Group Of Pictures for encoding.
-   ClientPort: the port on which the server listens the render requests from the clients.
-   LogPeriod: the period of logging.

Then, run the build and the server will start to listen to the scheduler and clients.

### Scheduler

The scheduler is in `Scheduler/`, with Go version 1.18.

The scheduler will read the the render server list `servers.csv`. Each line should contain three parameters, the first one is the IP address of the render server, the second one is the `ClientPort` of the render server, and the third one is the `ScheduleListenPort` of the render server. Note that there should be no whitespaces between elements.

Use `go run main.go` to test the program, and the scheduler will start to listen to RPC calls on port **50051**.
