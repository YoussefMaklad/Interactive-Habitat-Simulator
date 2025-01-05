import cam
from deepface import DeepFace


def detect_emotion(image):
    result = DeepFace.analyze(image,actions=['emotion'], enforce_detection=False)[0]
    dominant_emotion = result['dominant_emotion']
    return dominant_emotion

if __name__ == "__main__":
    _, image = cam.capture_image()
    result = detect_emotion(image)
    print(f"dominant emotion is: {result}")
    