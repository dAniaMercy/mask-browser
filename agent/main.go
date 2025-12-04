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
	"github.com/docker/go-connections/nat"
	"github.com/gorilla/mux"
	"github.com/streadway/amqp"
)

type Agent struct {
	dockerClient  *client.Client
	kafkaProducer *kafka.Producer
	rabbitMQConn  *amqp.Connection
	rabbitMQCh    *amqp.Channel
}

type ContainerTask struct {
	Action      string                 `json:"action"` // create, start, stop, delete
	ProfileID   int                    `json:"profileId"`
	ContainerID string                 `json:"containerId"`
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
		dockerClient:  dockerClient,
		kafkaProducer: kafkaProducer,
		rabbitMQConn:  rabbitConn,
		rabbitMQCh:    rabbitCh,
	}, nil
}

func (a *Agent) CreateBrowserContainer(profileID int, config map[string]interface{}) (string, error) {
	ctx := context.Background()

	// Exposed ports as nat.PortSet
	exposed := nat.PortSet{
		"8080/tcp": struct{}{},
	}

	containerConfig := &container.Config{
		Image:        "maskbrowser/browser:latest",
		Env:          []string{fmt.Sprintf("PROFILE_ID=%d", profileID)},
		ExposedPorts: exposed,
	}

	// PortBindings and Resources in HostConfig
	hostConfig := &container.HostConfig{
		PortBindings: nat.PortMap{
			"8080/tcp": []nat.PortBinding{
				{HostIP: "0.0.0.0", HostPort: "0"},
			},
		},
		Resources: container.Resources{
			Memory:     512 * 1024 * 1024, // 512 MB
			MemorySwap: 512 * 1024 * 1024,
			NanoCPUs:   500_000_000, // 0.5 CPU
		},
		RestartPolicy: container.RestartPolicy{
			Name: "unless-stopped",
		},
	}

	// Create container
	name := fmt.Sprintf("maskbrowser-profile-%d", profileID)
	resp, err := a.dockerClient.ContainerCreate(ctx, containerConfig, hostConfig, nil, nil, name)
	if err != nil {
		return "", err
	}

	// Start container
	if err := a.dockerClient.ContainerStart(ctx, resp.ID, types.ContainerStartOptions{}); err != nil {
		return "", err
	}

	// Publish to Kafka
	topic := "profile-events"
	event := map[string]interface{}{
		"eventType":   "ContainerCreated",
		"profileId":   profileID,
		"containerId": resp.ID,
		"timestamp":   time.Now().Unix(),
	}
	eventJSON, _ := json.Marshal(event)
	_ = a.kafkaProducer.Produce(&kafka.Message{
		TopicPartition: kafka.TopicPartition{Topic: &topic, Partition: kafka.PartitionAny},
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
			timeout := int(30)
			// ContainerStop expects container.StopOptions
			err := a.dockerClient.ContainerStop(ctx, task.ContainerID, container.StopOptions{Timeout: &timeout})
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
	defer func() {
		if agent.dockerClient != nil {
			_ = agent.dockerClient.Close()
		}
		if agent.kafkaProducer != nil {
			agent.kafkaProducer.Close()
		}
		if agent.rabbitMQConn != nil {
			_ = agent.rabbitMQConn.Close()
		}
		if agent.rabbitMQCh != nil {
			_ = agent.rabbitMQCh.Close()
		}
	}()

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
