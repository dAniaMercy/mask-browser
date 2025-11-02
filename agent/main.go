package main

import (
	"context"
	"encoding/json"
	"fmt"
	"log"
	"net/http"
	"os"
	"time"

	"github.com/confluentinc/confluent-kafka-go/v2/kafka"
	"github.com/docker/docker/api/types"
	"github.com/docker/docker/api/types/container"
	"github.com/docker/docker/client"
	"github.com/gorilla/mux"
	"github.com/streadway/amqp"
)

type Agent struct {
	dockerClient *client.Client
	kafkaProducer *kafka.Producer
	rabbitMQConn *amqp.Connection
	rabbitMQCh *amqp.Channel
}

type ContainerTask struct {
	Action      string `json:"action"`      // create, start, stop, delete
	ProfileID   int    `json:"profileId"`
	ContainerID string `json:"containerId"`
	Config      map[string]interface{} `json:"config"`
}

func NewAgent() (*Agent, error) {
	// Docker client
	dockerClient, err := client.NewClientWithOpts(client.FromEnv, client.WithAPIVersionNegotiation())
	if err != nil {
		return nil, err
	}

	// Kafka producer
	kafkaProducer, err := kafka.NewProducer(&kafka.ConfigMap{
		"bootstrap.servers": os.Getenv("KAFKA_BROKERS"),
	})
	if err != nil {
		return nil, err
	}

	// RabbitMQ connection
	rabbitURL := fmt.Sprintf("amqp://%s:%s@%s/",
		os.Getenv("RABBITMQ_USER"),
		os.Getenv("RABBITMQ_PASS"),
		os.Getenv("RABBITMQ_HOST"))
	
	rabbitConn, err := amqp.Dial(rabbitURL)
	if err != nil {
		return nil, err
	}

	rabbitCh, err := rabbitConn.Channel()
	if err != nil {
		return nil, err
	}

	return &Agent{
		dockerClient:   dockerClient,
		kafkaProducer:  kafkaProducer,
		rabbitMQConn:   rabbitConn,
		rabbitMQCh:     rabbitCh,
	}, nil
}

func (a *Agent) CreateBrowserContainer(profileID int, config map[string]interface{}) (string, error) {
	ctx := context.Background()

	containerConfig := &container.Config{
		Image: "maskbrowser/browser:latest",
		Env: []string{
			fmt.Sprintf("PROFILE_ID=%d", profileID),
		},
		ExposedPorts: map[string]struct{}{
			"8080/tcp": {},
		},
	}

	hostConfig := &container.HostConfig{
		PortBindings: map[string][]container.PortBinding{
			"8080/tcp": {{HostIP: "0.0.0.0", HostPort: "0"}},
		},
		Memory:     512 * 1024 * 1024, // 512MB
		MemorySwap: 512 * 1024 * 1024,
		NanoCPUs:   500_000_000, // 0.5 CPU
		RestartPolicy: container.RestartPolicy{
			Name: "unless-stopped",
		},
	}

	resp, err := a.dockerClient.ContainerCreate(ctx, containerConfig, hostConfig, nil, nil,
		fmt.Sprintf("maskbrowser-profile-%d", profileID))
	if err != nil {
		return "", err
	}

	err = a.dockerClient.ContainerStart(ctx, resp.ID, types.ContainerStartOptions{})
	if err != nil {
		return "", err
	}

	// Publish to Kafka
	event := map[string]interface{}{
		"eventType":  "ContainerCreated",
		"profileId":   profileID,
		"containerId": resp.ID,
		"timestamp":   time.Now().Unix(),
	}
	eventJSON, _ := json.Marshal(event)
	a.kafkaProducer.Produce(&kafka.Message{
		TopicPartition: kafka.TopicPartition{Topic: &[]string{"profile-events"}[0], Partition: kafka.PartitionAny},
		Value:          eventJSON,
	}, nil)

	return resp.ID, nil
}

func (a *Agent) HandleRabbitMQTasks() {
	msgs, err := a.rabbitMQCh.Consume(
		"container-tasks",
		"agent",
		true,
		false,
		false,
		false,
		nil,
	)
	if err != nil {
		log.Fatal(err)
	}

	for msg := range msgs {
		var task ContainerTask
		if err := json.Unmarshal(msg.Body, &task); err != nil {
			log.Printf("Error parsing task: %v", err)
			continue
		}

		switch task.Action {
		case "create":
			containerID, err := a.CreateBrowserContainer(task.ProfileID, task.Config)
			if err != nil {
				log.Printf("Error creating container: %v", err)
			} else {
				log.Printf("Created container %s for profile %d", containerID, task.ProfileID)
			}
		case "stop":
			ctx := context.Background()
			timeout := 30 * time.Second
			err := a.dockerClient.ContainerStop(ctx, task.ContainerID, &timeout)
			if err != nil {
				log.Printf("Error stopping container: %v", err)
			}
		case "delete":
			ctx := context.Background()
			err := a.dockerClient.ContainerRemove(ctx, task.ContainerID, types.ContainerRemoveOptions{
				Force: true,
			})
			if err != nil {
				log.Printf("Error deleting container: %v", err)
			}
		}
	}
}

func (a *Agent) HealthCheck(w http.ResponseWriter, r *http.Request) {
	w.WriteHeader(http.StatusOK)
	json.NewEncoder(w).Encode(map[string]string{"status": "ok"})
}

func main() {
	agent, err := NewAgent()
	if err != nil {
		log.Fatal(err)
	}
	defer agent.dockerClient.Close()
	defer agent.kafkaProducer.Close()
	defer agent.rabbitMQConn.Close()
	defer agent.rabbitMQCh.Close()

	// Start RabbitMQ consumer
	go agent.HandleRabbitMQTasks()

	// Health check endpoint
	r := mux.NewRouter()
	r.HandleFunc("/health", agent.HealthCheck).Methods("GET")

	port := os.Getenv("PORT")
	if port == "" {
		port = "8080"
	}

	log.Printf("Agent started on port %s", port)
	log.Fatal(http.ListenAndServe(":"+port, r))
}

