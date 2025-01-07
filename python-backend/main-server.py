import time
import cv2
import socket
import json
import csv
import numpy as np
import matplotlib
from rich import print
from collections import Counter
from threading import Thread
from thread_with_return_value import ThreadWithReturn
from face_recognization_funcs import detect_emotion, detect_face
from database import connect_to_database, save_user_average_emotion_to_database, insert_new_user
from heatmap import generate_heatmap
from encode import encode_all
from constants import *
import sqlite3
import bluetooth_scan
import cam

matplotlib.use("agg")

def register_new_user():
    cap = cv2.VideoCapture(0)
    while True:
        ret, frame = cap.read()
        frame = cv2.resize(frame, (480, 320))
        rgb = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
                
        cv2.putText(frame, 'Ready? Press "Q" ! :)', (100, 50), cv2.FONT_HERSHEY_SIMPLEX, 1.3, (0, 255, 0), 3, cv2.LINE_AA)
        cv2.imshow('frame', rgb)
        if cv2.waitKey(25) == ord('q'):
            break
    
    device = json.loads(bluetooth_scan.get_latest_connected_bluetooth_device())
    
    mac_address = device['device'][0]['address']
    name = device['device'][0]['name']
    image_path = f"./known-faces/{name}/{name}.jpg"
    
    insert_new_user(name, image_path, mac_address, "Kid")
    
    cv2.imwrite(image_path, rgb) 
    cap.release()
    cv2.destroyAllWindows()
    
    encode_all()
    
    return name

def set_proper_labels_according_to_user_role(role):
    global labels_dict
    labels_dict = labels_dict_kid if role == "Kid" else labels_dict_teacher

def initialize_gaze_coordinates_csv():
    with open(CSV_FILE, mode='w', newline='') as file:
        writer = csv.writer(file)
        writer.writerow(["Looking Direction", "X", "Y"])

def save_gaze_coordinates(looking_direction, x, y):
    with open(CSV_FILE, mode='a', newline='') as file:
        writer = csv.writer(file)
        writer.writerow([looking_direction, x, y])

def send_teacher_report(client_socket: socket.socket, db: sqlite3.Cursor):
    def format_experiences(experiences):
        formatted_experiences = {}
        for user_id, average_emotion, username in experiences:
            formatted_experiences[username] = average_emotion
        
        print("Teacher Report: \n", formatted_experiences)    
        return formatted_experiences

    try:
        experiences = db.execute("""
            SELECT e.user_id, e.average_emotion, u.name
            FROM experiences e
            JOIN users u ON e.user_id = u.user_id
        """).fetchall()        
        formatted_experiences = format_experiences(experiences)

        teacher_report_json = json.dumps(formatted_experiences)
        client_socket.sendall(f"TeacherReport:{teacher_report_json}\n".encode('utf-8'))
        print(f"Sent Teacher Report: {teacher_report_json} to C# client.")

    except sqlite3.Error as e:
        print(f"Error fetching or sending teacher report: {e}")

def authenticate_user(client_socket: socket.socket):
    recognized_username = None
    user_id, username, role = None, None, None
    
    while not recognized_username:
        _, image = cam.capture_image()
        face_recognition_thread = ThreadWithReturn(target=detect_face, args=(image,))
        face_recognition_thread.start()
        result = face_recognition_thread.join()
        
        if "can't identify" not in result:
            recognized_username = result
            print(f"Detected user: {recognized_username}")
        else:
            print(result)
            # print("registrating a new user...")
            # new_user_name = register_new_user()
            # recognized_username = new_user_name
            # print(f"user: {new_user_name} has been registered successfully!!!")
              

    db, db_connection = connect_to_database()
    
    db.execute("SELECT user_id, name, role, mac_address FROM users")
    users = db.fetchall()
    print(f"Users: {users}")
    
    connected_bluetooth_devices = json.loads(bluetooth_scan.get_connected_bluetooth_devices())
    connected_mac_addresses = {device["address"] for device in connected_bluetooth_devices.get("devices", [])}
    print(f"Connected macs: {connected_mac_addresses}")
    
    for user_id, name, role, mac_address in users:
        try:
            if mac_address not in connected_mac_addresses:
                continue

            if recognized_username == name:
                user_id, username, role = user_id, name, role
                
                client_socket.send(f"Identity:{role}\n".encode('utf-8'))
                print(f"Authenticated via Bluetooth and Face Recognition: {name} ({role})")
                print(f"Sent Identity: {role} to C# client.")
                
                if role == "Teacher":
                    send_teacher_report(client_socket, db)
                
                if role == "Kid":
                    print(f"Kid ID: {user_id}")
                    print(f"Kid Name: {name}")
                                        
                db_connection.close()    
                return user_id, username, role
            
        except Exception as e:
            print(f"Error processing authentication for {name}: {e}")

def recognize_emotions(frame):
    emotion_detection_thread = ThreadWithReturn(target=detect_emotion, args=(frame,))
    emotion_detection_thread.start()
    detected_emotion = emotion_detection_thread.join()
    print(f"Detected emotion: {detected_emotion}")
    
    if detected_emotion and detected_emotion in emotions:
        emotion_buffer.append(detected_emotion)
        
def recognize_and_send_animals(frame, client_socket: socket.socket):
    yolo_results = yolo_model.predict(frame, conf=0.25, verbose=False)[0]
    for detection in yolo_results.boxes:
        cls_ = int(detection.cls[0])
        label = yolo_model.names[cls_]
        if label in animals:
            bbox = detection.xyxy[0]
            x1, y1, x2, y2 = map(int, bbox)
            confidence = detection.conf[0]
            # cv2.rectangle(frame, (x1, y1), (x2, y2), (255, 0, 0), 2)
            # cv2.putText(frame, f"{label} ({confidence:.2f})", (x1, y1 - 10),
            #             cv2.FONT_HERSHEY_SIMPLEX, 0.5, (255, 0, 0), 2)
            try:
                client_socket.send(f"Animal:{label}\n".encode('utf-8'))
                print(f"Sent Animal: {label} to C# client.")
                time.sleep(2.5)
            except Exception as e:
                print("Error sending object data to C# client:", e)
                break

def get_gaze_frame_and_save_looking_direction(frame):
    gaze.refresh(frame)
    gaze_frame = gaze.annotated_frame()
    
    text = ""
    if gaze.is_blinking():
        text = "Blinking"
    elif gaze.is_right():
        text = "Looking right"
    elif gaze.is_left():
        text = "Looking left"
    elif gaze.is_center():
        text = "Looking center"
        
    if text == "":
        text = "could not detect gaze"    
        
    left_pupil = gaze.pupil_left_coords()
    right_pupil = gaze.pupil_right_coords()
    
    if left_pupil and right_pupil:
        average_x = (left_pupil[0] + right_pupil[0]) / 2
        average_y = (left_pupil[1] + right_pupil[1]) / 2
        save_gaze_coordinates(text, average_x, average_y) 
    
    return gaze_frame, text        

def recognize_and_send_gestures(frame, mp_results, client_socket: socket.socket):
    data_aux = []
    x_ = []
    y_ = []

    H, W, _ = frame.shape

    if mp_results.multi_hand_landmarks:
        for hand_landmarks in mp_results.multi_hand_landmarks:
            mp_drawing.draw_landmarks(
                frame,
                hand_landmarks,
                mp_hands.HAND_CONNECTIONS,
                mp_drawing_styles.get_default_hand_landmarks_style(),
                mp_drawing_styles.get_default_hand_connections_style()
            )

        for hand_landmarks in mp_results.multi_hand_landmarks:
            for i in range(len(hand_landmarks.landmark)):
                x = hand_landmarks.landmark[i].x
                y = hand_landmarks.landmark[i].y

                x_.append(x)
                y_.append(y)

            for i in range(len(hand_landmarks.landmark)):
                x = hand_landmarks.landmark[i].x
                y = hand_landmarks.landmark[i].y
                data_aux.append(x - min(x_))
                data_aux.append(y - min(y_))

        prediction = model.predict([np.asarray(data_aux)])
        predicted_character = labels_dict[int(prediction[0])]
        
        if predicted_character in ["Rotate", "Select", "Back"]:
            client_socket.send(f"Gesture:{predicted_character}\n".encode('utf-8'))
            print(f"Sent Gesture: {predicted_character} to C# client.")
            
        elif predicted_character in ["Farm", "WildLife", "Home"]:
            client_socket.send(f"Habitat:{predicted_character}\n".encode('utf-8'))
            print(f"Sent Habitat: {predicted_character} to C# client.")
            
        elif predicted_character in ["HappyKids", "SadKids", "NeutralKids", "AngryKids", "FearKids"]:
            client_socket.send(f"ReportType:{predicted_character}\n".encode('utf-8'))
            print(f"Sent ReportType: {predicted_character} to C# client.")
            
    time.sleep(2)          
        
def main_loop(client_socket: socket.socket, role):
    cap = cv2.VideoCapture(0, cv2.CAP_DSHOW)

    while cap.isOpened():
        ret, frame = cap.read()
        if not ret:
            print("Can't receive frame (stream end?). Exiting...")
            break

        frame = cv2.resize(frame, (480, 320))
        
        try:
            rgb_frame = cv2.cvtColor(frame, cv2.COLOR_BGR2RGB)
            results = hands.process(rgb_frame)

            if results.multi_hand_landmarks:
                recognize_and_send_gestures(rgb_frame, results, client_socket)
            
            if role == "Kid":
                gaze_frame, looking_direction = get_gaze_frame_and_save_looking_direction(frame)    
                recognize_and_send_animals(frame, client_socket)        
                recognize_emotions(frame)
                cv2.putText(gaze_frame, looking_direction, (90, 60), cv2.FONT_HERSHEY_DUPLEX, 1.6, (147, 58, 31), 2)
                cv2.imshow("Server Frame", gaze_frame)
            else:
                cv2.imshow("Server Frame", rgb_frame)    

        except Exception as e:
            print(f"Error: {e}")

        if cv2.waitKey(1) == ord('q'):
            break

    cap.release()
    cv2.destroyAllWindows()

def start_socket_server(host='localhost', port=5000):
    server_socket = socket.socket(socket.AF_INET, socket.SOCK_STREAM)
    server_socket.bind((host, port))
    server_socket.listen(1)
    print("Socket server started, waiting for connection...")
    client_socket, address = server_socket.accept()
    print(f"Connected to C# client at {address}")
    return client_socket, server_socket

def main_socket_thread():
    initialize_gaze_coordinates_csv()
    client_socket, server_socket = start_socket_server()
    user_id, username, role = authenticate_user(client_socket)
    set_proper_labels_according_to_user_role(role)
    
    main_loop(client_socket, role)
    
    if role == "Kid":
        print(f"saving to db average emotion of {Counter(emotion_buffer).most_common(1)[0][0]} for the user: {username}, with role of {role} ....")
        save_user_average_emotion_to_database(user_id, Counter(emotion_buffer).most_common(1)[0][0])
        print("Generating heatmap for gaze data...")
        generate_heatmap(CSV_FILE)
        
    client_socket.close()
    server_socket.close()
    print("Socket server closed.")

if __name__ == "__main__":
    thread = Thread(target=main_socket_thread)
    thread.start()
