# load_data.py
import pandas as pd
from db_connection import fetch_data

BOOK_MAP = {}  # dict: { "B1": "Tên sách 1", "B2": "Tên sách 2", ... }

def load_interactions():
    query = """
        SELECT ui.UserId, ui.BookId, ui.Rating, ui.Score, b.BookName
        FROM UserInteractions ui
        JOIN Book b ON ui.BookId = b.Id
    """
    df = fetch_data(query)

    if df is None or df.empty:
        print("❌ Không có dữ liệu trong UserInteractions")
        return None

    df = df.rename(columns={
        "UserId": "UserID",
        "BookId": "BookID",
        "BookName": "BookName"
    })

    # BookID dạng Bxx
    df["BookID"] = df["BookID"].apply(lambda x: f"B{x}")

    # Tạo map BookID -> BookName
    global BOOK_MAP
    BOOK_MAP = dict(zip(df["BookID"], df["BookName"]))

    return df

if __name__ == "__main__":
    df_interactions = load_interactions()
    if df_interactions is not None:
        print("✅ Dataset sau khi load:")
        print(df_interactions.head())
