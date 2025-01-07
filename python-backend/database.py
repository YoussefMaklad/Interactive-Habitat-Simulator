from rich import print
import sqlite3

def connect_to_database():
    db_connection = sqlite3.connect('./database.db')
    db = db_connection.cursor()
    return db, db_connection

def create_database():
    connection = sqlite3.connect('./database.db')
    db = connection.cursor()
    
    db.execute(''' 
    CREATE TABLE IF NOT EXISTS users (
        user_id INTEGER PRIMARY KEY,
        name TEXT NOT NULL,
        role TEXT NOT NULL, 
        mac_address TEXT NOT NULL,
        image_path TEXT NOT NULL
    )
    ''')

    db.execute(''' 
    CREATE TABLE IF NOT EXISTS experiences (
        experience_id INTEGER PRIMARY KEY,
        user_id INTEGER NOT NULL,
        average_emotion TEXT,
        FOREIGN KEY (user_id) REFERENCES users(user_id)
    )
    ''')
    
    connection.commit()
    connection.close()

def save_user_average_emotion_to_database(user_id: int, average_emotion: str):
    db, db_connection = connect_to_database()
    db.execute('''
                INSERT INTO experiences (user_id, average_emotion)
                VALUES (?, ?)
                ''', (user_id, average_emotion))
    db_connection.commit()
    db_connection.close()
    print(f"User with ID: {user_id}, Average Emotion: {average_emotion} has been saved to the database succesfully!")

def insert_known_users(db: sqlite3.Cursor, db_connection: sqlite3.Connection):
    known_users = [
        ("noha", "Kid", "CC:6B:1E:80:F5:85", "./known-faces/noha/noha.jpg"),
        ("youssef", "Kid", "94:5C:9A:97:15:10", "./known-faces/youssef/youssef.jpg"),
        ("seif", "Teacher", "24:5E:48:D6:C5:C6", "./known-faces/seif/seif.jpg"),
    ]

    try:
        for name, role, mac_address, image_path in known_users:
            db.execute(
                '''
                INSERT INTO users (name, role, mac_address, image_path)
                VALUES (?, ?, ?, ?)
                ''',
                (name, role, mac_address, image_path)
            )

        db_connection.commit()
        db_connection.close()
        
        print("Known Users inserted successfully.")
    except sqlite3.Error as e:
        print(f"Error inserting users: {e}") 
    finally:
        db_connection.close()

def insert_new_user(name: str, image_path: str, mac: str = "", role: str = "Kid"):
    db, db_connection = connect_to_database()
    db.execute(
                '''
                INSERT INTO users (name, role, mac_address, image_path)
                VALUES (?, ?, ?, ?)
                ''',
                (name, role, mac, image_path)
            )

    db_connection.commit()
    db_connection.close()
        
    print(f"User: {name} inserted to db successfully.")

if __name__ == "__main__":
    create_database()
    db, db_connection = connect_to_database()
    insert_known_users(db, db_connection)
    db_connection.close()
