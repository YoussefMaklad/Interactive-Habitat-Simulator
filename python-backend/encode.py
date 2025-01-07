import os
import face_recognition

pic_dir = "./known-faces"

def encode_all():
    for person_name in os.listdir(pic_dir):
        person_path = os.path.join(pic_dir, person_name)
        if os.path.isdir(person_path):
            encodings = []
            for path, folders, files in os.walk(person_path):
                for filename in files:
                    file_path = os.path.join(path, filename)
                    try:
                        image = face_recognition.load_image_file(file_path)
                        face_encs = face_recognition.face_encodings(image)
                        if face_encs:
                            encodings.append(face_encs[0])
                    except Exception as e:
                        print(f"Error processing file {file_path}: {e}")

            with open(f"encodings/{person_name}.csv", "w") as file:
                for encoding in encodings:
                    string = [str(val) for val in encoding.tolist()]
                    file.write(",".join(string))
                    file.write("\n")
                    
if __name__ == "__main__":
    encode_all()                    
