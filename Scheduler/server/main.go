package main

import (
	"context"
	"encoding/csv"
	"log"
	//"time"
	"net"
	"os"
	"strconv"

	pb_recieve "scheduler/assignRenderer"
	pb_send "scheduler/openSession"

	"google.golang.org/grpc"
	"google.golang.org/grpc/credentials/insecure"
	//"google.golang.org/grpc/reflection"
)

// server is used to implement helloworld.GreeterServer.
type server struct {
	pb_recieve.UnimplementedAssignorServer
}

var servers [][]string

func readServers() {
	file, err := os.Open("servers.csv")
	if err != nil {
		log.Fatal(err)
	}
	defer file.Close()

	csvReader := csv.NewReader(file)
	servers, err = csvReader.ReadAll()
	if err != nil {
		log.Fatal("Unable to parse file as CSV", err)
	}
}

func selectServer(exclude []string) (string, string, string) {
	for _, server := range servers {
		excluded := false
		for _, exclude := range exclude {
			host, port, err := net.SplitHostPort(exclude)
			if err != nil {
				log.Printf("Could not split host port: %v", err)
				continue
			}

			if server[0] == host && server[1] == port {
				log.Printf("exclude: %v", exclude)
				excluded = true
				break
			}
		}
		if !excluded {
			return server[0], server[1], server[2]
		}
	}
	return "", "", ""
}

// SayHello implements helloworld.GreeterServer
func (s *server) Request(ctx_recieve context.Context, in *pb_recieve.ClientRequest) (*pb_recieve.ServerInfo, error) {
	log.Printf("Received: ver=%v, res_x=%v, res_y=%v", in.GetVersion(), in.GetResX(), in.GetResY())

	host, rendPort, shedPort := selectServer(in.GetExServers())
	if host == "" {
		return &pb_recieve.ServerInfo{Status: 1, Host: host, Port: 0, SessionId: 0}, nil
	}

	log.Printf("Selected: %v:%v", host, rendPort)
	conn, err := grpc.Dial(host + ":" + shedPort, grpc.WithTransportCredentials(insecure.NewCredentials()))
	if err != nil {
		log.Printf("did not connect: %v", err)
	}
	defer conn.Close()
	c := pb_send.NewOpenSessionClient(conn)
	//ctx_send, cancel := context.WithTimeout(context.Background(), time.Second)
	//defer cancel()
	ctx_send := context.Background()
	r, err := c.Request(ctx_send, &pb_send.SchedulerRequest{Version: in.GetVersion(), ResX: in.GetResX(), ResY: in.GetResY()})
	if err != nil {
		log.Printf("could not assign: %v", err)
	}

	port, err := strconv.Atoi(rendPort)
	if (err != nil) {
		log.Printf("Could not convert: %v", rendPort)
	}

	log.Printf("Assigning to server %v: status=%v id=%v", host, r.GetStatus(), r.GetSessionId())

	return &pb_recieve.ServerInfo{Status: r.GetStatus(), Host: host, Port: int32(port), SessionId: r.GetSessionId()}, nil
}

func main() {
	readServers()

	lis, err := net.Listen("tcp", ":50051")
	if err != nil {
		log.Fatalf("failed to listen: %v", err)
	}
	s := grpc.NewServer()
	pb_recieve.RegisterAssignorServer(s, &server{})
	log.Printf("server listening at %v", lis.Addr())
	//reflection.Register(s)
	if err := s.Serve(lis); err != nil {
		log.Fatalf("failed to serve: %v", err)
	}
}
