import face_recognition
import numpy as np
import cam
import os


def read_encodings(csv_dir: str):
    encodings = {}
    for path, folders, files in os.walk(csv_dir):
        for filename in files:
            name = filename.split(".")[0]
            encodings[name] = []
            with open(os.path.join(csv_dir, filename)) as file:
                encoded_strings = file.readlines()
                for string in encoded_strings:
                    string = string.replace("\n","")
                    encodings[name].append(np.asarray(string.split(","),dtype=np.float64))
    return encodings


def determine_whos_in_the_pic(comparison_results) -> bool:
    acceptance_count = comparison_results.count(np.True_)
    if acceptance_count / len(comparison_results) >= 0.75:
        return True
    return False


def recognize_face(image,encodings)-> str:
    """
        takes in image and encodings, returns who is in the picture
    """
    image_locations = face_recognition.face_locations(image)
    unknown_encoding = face_recognition.face_encodings(image,image_locations)
    if len(unknown_encoding)<=0:
        return "can't find faces in provided picture"

    unknown_encoding = unknown_encoding[0]

    for name in encodings:
        results = face_recognition.compare_faces(encodings[name], unknown_encoding, tolerance=0.5)
        acceptance = determine_whos_in_the_pic(results)
        if acceptance:
            return f"{name}"
    return "can't identify the person in the picture"



if __name__ == "__main__":
    _, image = cam.capture_image()
    encodings = read_encodings("./encodings/")
    print(recognize_face(image,encodings))
