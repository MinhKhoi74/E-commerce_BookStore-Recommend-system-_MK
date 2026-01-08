# db_connection.py
import pyodbc
import pandas as pd

# ===== Thông tin kết nối =====
server = r"MINHKHOI\SQLEXPRESS"
database = "BookShoppingCartMvcMau"
username = "sa"
password = "123"
port = "1433"

# ===== Chuỗi kết nối =====
connection_string = (
    f"DRIVER={{ODBC Driver 17 for SQL Server}};"
    f"SERVER={server},{port};"
    f"DATABASE={database};"
    f"UID={username};"
    f"PWD={password};"
    "TrustServerCertificate=yes;"
)

def get_connection():
    try:
        conn = pyodbc.connect(connection_string)
        print("✅ Kết nối thành công tới SQL Server!")
        return conn
    except Exception as e:
        print("❌ Lỗi kết nối:", e)
        return None

def fetch_data(query):
    conn = get_connection()
    if conn:
        df = pd.read_sql(query, conn)
        conn.close()
        return df
    else:
        return None
