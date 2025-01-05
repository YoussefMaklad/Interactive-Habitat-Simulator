import pickle
import mediapipe as mp
from ultralytics import YOLO
from GazeTracking.gaze_tracking import GazeTracking

mp_drawing_styles = mp.solutions.drawing_styles
mp_drawing = mp.solutions.drawing_utils
mp_hands = mp.solutions.hands

hands = mp_hands.Hands(
    static_image_mode=True,
    max_num_hands=1,
    min_detection_confidence=0.3,
    min_tracking_confidence=0.3
)

yolo_model = YOLO("./yolo11n.pt", verbose=False)
animals = {"cat", "dog", "bird", "horse", "sheep", "giraffe", "bear", "zebra", "elephant", "cow"}

emotion_buffer = []
emotions = ["happy", "sad", "angry", "neutral"]

model_dict = pickle.load(open('./gestures_training/classifier.p', 'rb'))
model = model_dict['classifier']
labels_dict = {0: 'Rotate', 1: 'Home', 2: 'Farm', 3: 'WildLife', 4: 'Select'}

KID_ID = None
CSV_FILE = "gaze_coordinates.csv"
gaze = GazeTracking()