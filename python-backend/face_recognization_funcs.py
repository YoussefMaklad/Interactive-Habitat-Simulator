import recognize
import emotion

def detect_face(image):
    encodings = recognize.read_encodings("encodings/")
    result = recognize.recognize_face(image,encodings)
    return result

def detect_emotion(image):
    result = emotion.detect_emotion(image)
    return result