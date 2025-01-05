import sqlite3
import json
from database import connect_to_database


def view_teacher_report(db: sqlite3.Cursor):
    def format_experiences(experiences):
        formatted_experiences = {}
        for user_id, average_emotion, username in experiences:
            formatted_experiences[username] = average_emotion
        
        # print("Teacher Report: \n", formatted_experiences)    
        return formatted_experiences

    try:
        experiences = db.execute("""
            SELECT e.user_id, e.average_emotion, u.name
            FROM experiences e
            JOIN users u ON e.user_id = u.user_id
        """).fetchall()
        
        # print("experiences: ", experiences)
        
        formatted_experiences = format_experiences(experiences)

        teacher_report_json = json.dumps(formatted_experiences)
        print(teacher_report_json)
        
    except Exception as e:
        print(f"Error fetching or sending teacher report: {e}")    
     

def query_database_for_experiences(db: sqlite3.Cursor):
    try:
        experiences = db.execute("""
            SELECT * FROM experiences
        """).fetchall()
        
        print("experiences: ", experiences)
        
    except Exception as e:
        print(f"Error fetching or sending teacher report: {e}")     
        
        
if __name__ == "__main__":
    db, db_connection = connect_to_database()
    view_teacher_report(db)
    query_database_for_experiences(db)    
    db_connection.close()
    
