# main.py
from fastapi import FastAPI
import random
import numpy as np
import hashlib
import pandas as pd
import user_cf
import item_cf
import matrix_factorization as mf
from load_data import BOOK_MAP, load_interactions
from evaluate import evaluate_model, select_best_model, train_test_split

# ==================== 0) Fix seed để reproduce kết quả ====================
SEED = 42
random.seed(SEED)
np.random.seed(SEED)


# ==================== 1) Model Manager ====================
class ModelManager:
    def __init__(self, seed=42):
        self.seed = seed
        self.current_hash = None
        self.best_model_name = None
        self.best_model = None
        self.all_metrics = {}

    def compute_hash(self, df: pd.DataFrame):
        """Tạo hash từ dữ liệu interactions."""
        data_bytes = df.to_csv(index=False).encode()
        return hashlib.md5(data_bytes).hexdigest()

    def train_models(self, df: pd.DataFrame):
        print(">>> Training models with updated data...")

        # Chia train/test
        train_df, test_df = train_test_split(df, test_ratio=0.2, seed=self.seed)

        # Train các model
        print("Training Matrix Factorization...")
        mf.train(train_df, k=3, n_iter=20)

        print("Building ItemCF model...")
        item_cf.build_model(train_df)

        # Evaluate từng model
        metrics_user = evaluate_model(user_cf, train_df, test_df, task="ranking")
        metrics_user.update(evaluate_model(user_cf, train_df, test_df, task="rating"))

        metrics_item = evaluate_model(item_cf, train_df, test_df, task="ranking")
        metrics_item.update(evaluate_model(item_cf, train_df, test_df, task="rating"))

        metrics_mf = evaluate_model(mf, train_df, test_df, task="ranking")
        metrics_mf.update(evaluate_model(mf, train_df, test_df, task="rating"))

        self.all_metrics = {
            "user_cf": metrics_user,
            "item_cf": metrics_item,
            "mf": metrics_mf
        }

        # Chọn best model
        self.best_model_name, self.best_model = select_best_model(
            self.all_metrics, criterion="NDCG@K", task="ranking"
        )
        print(f">>> Best model selected: {self.best_model_name}")

    def reload_if_needed(self):
        """Reload và retrain nếu database thay đổi."""
        df = load_interactions()
        new_hash = self.compute_hash(df)
        if new_hash != self.current_hash:
            print(">>> Database changed, retraining models...")
            self.current_hash = new_hash
            self.train_models(df)
        else:
            print(">>> Database unchanged, using cached model.")
        return self.best_model_name, self.best_model


# ==================== 2) Khởi động Model Manager ====================
manager = ModelManager(seed=SEED)
manager.reload_if_needed()


# ==================== 3) FastAPI ====================
app = FastAPI(title="Recommendation API")


# ===== Endpoint cho model tốt nhất =====
@app.get("/recommend/best/{user_id}")
def recommend_best(user_id: str, top_n: int = 10):
    best_model_name, best_model = manager.reload_if_needed()
    recs = best_model.recommend_top_n(user_id, top_n=top_n)
    results = [
        {
            "bookId": int(book.replace("B", "")),
            "bookName": BOOK_MAP.get(book, "Unknown"),
            "score": float(score)
        }
        for book, score in recs
    ]
    return {"model": best_model_name, "user": user_id, "recommendations": results}


# ===== Endpoint riêng lẻ nếu cần =====
@app.get("/recommend/usercf/{user_id}")
def recommend_usercf(user_id: str, top_n: int = 10, alpha: float = 0.6):
    manager.reload_if_needed()
    recs = user_cf.recommend_top_n(user_id, top_n=top_n, alpha=alpha)
    results = [
        {
            "bookId": int(book.replace("B", "")),
            "bookName": BOOK_MAP.get(book, "Unknown"),
            "score": float(score)
        }
        for book, score in recs
    ]
    return {"model": "UserCF", "user": user_id, "recommendations": results}


@app.get("/recommend/itemcf/{user_id}")
def recommend_itemcf(user_id: str, top_n: int = 10, alpha: float = 0.6):
    manager.reload_if_needed()
    recs = item_cf.recommend_top_n(user_id, top_n=top_n, alpha=alpha)
    results = [
        {
            "bookId": int(book.replace("B", "")),
            "bookName": BOOK_MAP.get(book, "Unknown"),
            "score": float(score)
        }
        for book, score in recs
    ]
    return {"model": "ItemCF", "user": user_id, "recommendations": results}


@app.get("/recommend/mf/{user_id}")
def recommend_mf(user_id: str, top_n: int = 10):
    manager.reload_if_needed()
    recs = mf.recommend_top_n(user_id, n=top_n)
    results = [
        {
            "bookId": int(book.replace("B", "")),
            "bookName": BOOK_MAP.get(book, "Unknown"),
            "score": float(score)
        }
        for book, score in recs
    ]
    return {"model": "MatrixFactorization", "user": user_id, "recommendations": results}
# uvicorn main:app --reload
