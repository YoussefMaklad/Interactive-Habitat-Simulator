import cv2

def capture_image():
    cam = cv2.VideoCapture(0)

    _, image= cam.read()
    cam.release()
    cv2.destroyAllWindows()
    return _, image