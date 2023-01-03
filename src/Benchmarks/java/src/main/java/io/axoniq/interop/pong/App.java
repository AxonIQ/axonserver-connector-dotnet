package io.axoniq.interop.pong;

import com.google.protobuf.ByteString;
import io.axoniq.axonserver.connector.AxonServerConnection;
import io.axoniq.axonserver.connector.AxonServerConnectionFactory;
import io.axoniq.axonserver.connector.command.CommandChannel;
import io.axoniq.axonserver.connector.impl.ServerAddress;
import io.axoniq.axonserver.grpc.SerializedObject;
import io.axoniq.axonserver.grpc.command.Command;
import io.axoniq.axonserver.grpc.command.CommandResponse;
import org.slf4j.Logger;
import org.slf4j.LoggerFactory;

import java.util.Scanner;
import java.util.UUID;
import java.util.concurrent.CompletableFuture;
import java.util.concurrent.atomic.AtomicInteger;
import java.util.concurrent.atomic.AtomicLong;

public class App 
{
    private static final Logger logger = LoggerFactory.getLogger(App.class);

    private static CompletableFuture<CommandResponse> handle(Command command) {
        String ping = command.getPayload().getData().toStringUtf8();
        String id = ping.substring("ping=".length());
        logger.info("Receive command with ping id {}", id);
        return CompletableFuture.completedFuture(
                CommandResponse.newBuilder()
                               .setMessageIdentifier(UUID.randomUUID().toString())
                               .setRequestIdentifier(command.getMessageIdentifier())
                               .setPayload(
                                    SerializedObject
                                        .newBuilder()
                                        .mergeFrom(command.getPayload())
                                        .setData(ByteString.copyFromUtf8("pong=" + id)).build()).build());
    }

    public static void main(String[] args) {
        logger.info("Server: {}", args[0]);
        logger.info("Port: {}", args[1]);
        AxonServerConnectionFactory testSubject = 
            AxonServerConnectionFactory.forClient("java-client")
                .routingServers(new ServerAddress(args[0], Integer.parseInt(args[1])))
                .token("")
                .build();
        AxonServerConnection contextConnection = testSubject
                .connect("default");

        CommandChannel commandChannel = contextConnection.commandChannel();

        AtomicInteger counter = new AtomicInteger();
        AtomicLong timer = new AtomicLong();
        try {
            commandChannel.registerCommandHandler(command -> {
                logger.info("Receive command with name {}", command.getName());
                if (counter.updateAndGet(t -> t == 99 ? 0 : t + 1) == 0) {
                    long now = System.currentTimeMillis();
                    long previous = timer.getAndSet(now);
                    logger.info("Handled another 100 in {} ms", now - previous);
                }
                return handle(command);
            }, 100, "ping");

            new Scanner(System.in).nextLine();
        } catch(Exception e) {
            logger.error(e.toString());
        } finally {
            testSubject.shutdown();
        }
    }
}
