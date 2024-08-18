import cv2
from fer import FER
import socket

def main():
    # Initialize video capture
    cap = cv2.VideoCapture(0)
    detector = FER()

    # Set up the socket server to listen for incoming connections
    server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server_address = ('localhost', 8080)
    server_socket.bind(server_address)
    server_socket.listen(1)  # Listen for one connection
    print("Waiting for Unity to connect...")

    connection = None
    try:
        # Wait for Unity to connect
        connection, client_address = server_socket.accept()
        print(f"Connected to Unity: {client_address}")

        while True:
            # Capture frame-by-frame
            ret, frame = cap.read()
            if not ret:
                print("Failed to grab frame")
                break

            # Analyze emotions in the frame
            emotions = detector.detect_emotions(frame)

            # If emotions are detected, find the most prominent emotion
            if emotions:
                emotion = emotions[0]['emotions']
                predominant_emotion = max(emotion, key=emotion.get)
                emotion_score = emotion[predominant_emotion]
                happiness_percentage = emotion_score * 100  # Scale from 0 to 100

                # Print the detected emotion and percentage in the console
                print(f"Detected Emotion: {predominant_emotion}, Score: {happiness_percentage:.1f}%")

                # Send the emotion and percentage to Unity
                data = f"{predominant_emotion}:{happiness_percentage:.1f}"
                try:
                    connection.sendall(data.encode('utf-8'))
                except BrokenPipeError:
                    print("Connection broken. Exiting...")
                    break

            # Display the resulting frame
            cv2.imshow('Emotion Detector', frame)

            # Break the loop on 'q' key press
            if cv2.waitKey(1) & 0xFF == ord('q'):
                break

    except Exception as e:
        print(f"An error occurred: {e}")
    finally:
        # Release the video capture and close windows
        cap.release()
        cv2.destroyAllWindows()
        if connection:
            connection.close()  # Close the connection
        server_socket.close()  # Close the server

if __name__ == "__main__":
    main()
