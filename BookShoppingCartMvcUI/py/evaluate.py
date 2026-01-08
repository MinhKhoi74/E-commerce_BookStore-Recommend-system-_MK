"""
evaluate.py (nâng cấp)
- Split train/test
- Metrics: RMSE, MAE, Precision@K, Recall@K, NDCG@K
- Evaluate 3 models: UserCF, ItemCF, MF
- Compute dynamic ensemble weights dựa trên hiệu năng rating/ranking
- Recommend top-N kết hợp 3 mô hình
- Chọn model tốt nhất dựa trên metric
"""

import random
import numpy as np
import pandas as pd
from typing import List
from load_data import load_interactions
import user_cf
import item_cf
import matrix_factorization as mf

# ==================== 1) Train/Test split ====================
def train_test_split(df: pd.DataFrame, test_ratio: float = 0.2, seed: int = 42):
    random.seed(seed)
    train_rows, test_rows = [], []

    for user, group in df.groupby("UserID"):
        interactions = group.to_dict("records")
        n_test = max(1, int(len(interactions) * test_ratio))
        test_idx = set(random.sample(range(len(interactions)), n_test))
        for i, row in enumerate(interactions):
            if i in test_idx:
                test_rows.append(row)
            else:
                train_rows.append(row)

    return pd.DataFrame(train_rows), pd.DataFrame(test_rows)

# ==================== 2) Metrics ====================
def rmse(preds: List[float], targets: List[float]) -> float:
    preds = np.array(preds)
    targets = np.array(targets)
    return float(np.sqrt(((preds - targets) ** 2).mean()))

def mae(preds: List[float], targets: List[float]) -> float:
    preds = np.array(preds)
    targets = np.array(targets)
    return float(np.abs(preds - targets).mean())

def precision_at_k(recommended: List[str], relevant: List[str], k: int) -> float:
    if not recommended: return 0.0
    rec_k = recommended[:k]
    return len(set(rec_k) & set(relevant)) / k

def recall_at_k(recommended: List[str], relevant: List[str], k: int) -> float:
    if not relevant: return 0.0
    rec_k = recommended[:k]
    return len(set(rec_k) & set(relevant)) / len(relevant)

def ndcg_at_k(recommended: List[str], relevant: List[str], k: int) -> float:
    rec_k = recommended[:k]
    dcg = 0.0
    for idx, item in enumerate(rec_k):
        if item in relevant:
            dcg += 1.0 / np.log2(idx + 2)
    idcg = sum(1.0 / np.log2(i + 2) for i in range(min(len(relevant), k)))
    return dcg / idcg if idcg > 0 else 0.0

# ==================== 3) Evaluate function ====================
def evaluate_model(model, train_df: pd.DataFrame, test_df: pd.DataFrame,
                   top_k: int = 10, task: str = "ranking"):
    users = test_df["UserID"].unique()
    results = {}

    if task == "rating":
        preds, trues = [], []
        for _, row in test_df.iterrows():
            u, i, r = row["UserID"], row["BookID"], row.get("Rating", None)
            if pd.isna(r): continue
            try:
                pred = model.predict_rating(u, i)
            except Exception:
                continue
            if pred is not None:
                preds.append(pred)
                trues.append(r)
        if preds:
            results["RMSE"] = rmse(preds, trues)
            results["MAE"] = mae(preds, trues)

    elif task == "ranking":
        precisions, recalls, ndcgs = [], [], []
        for u in users:
            relevant = test_df[test_df["UserID"] == u]["BookID"].tolist()
            if not relevant: continue
            try:
                recs = model.recommend_top_n(u, top_n=top_k)
            except Exception:
                recs = []
            rec_items = [it for it, _ in recs]
            precisions.append(precision_at_k(rec_items, relevant, top_k))
            recalls.append(recall_at_k(rec_items, relevant, top_k))
            ndcgs.append(ndcg_at_k(rec_items, relevant, top_k))

        results["Precision@K"] = float(np.mean(precisions)) if precisions else 0.0
        results["Recall@K"] = float(np.mean(recalls)) if recalls else 0.0
        results["NDCG@K"] = float(np.mean(ndcgs)) if ndcgs else 0.0

    return results

# ==================== 4) Ensemble dynamic ====================
def compute_ensemble_weights(metrics_dict):
    rating_scores = {}
    ranking_scores = {}
    for name, metrics in metrics_dict.items():
        rating_scores[name] = 1.0 / metrics.get("RMSE", 1e5)
        ranking_scores[name] = metrics.get("NDCG@K", 0.0)

    total_rating = sum(rating_scores.values())
    total_rank = sum(ranking_scores.values())
    rating_weights = {k: v / total_rating for k, v in rating_scores.items()}
    ranking_weights = {k: v / total_rank for k, v in ranking_scores.items()}

    alpha = 0.5
    ensemble_weights = {k: alpha * rating_weights[k] + (1 - alpha) * ranking_weights[k]
                        for k in metrics_dict.keys()}
    return ensemble_weights

def ensemble_recommend(user, top_n: int, weights: dict):
    preds = {}
    models = {"user_cf": user_cf, "item_cf": item_cf, "mf": mf}
    for name, model in models.items():
        try:
            recs = model.recommend_top_n(user, top_n=10)
        except Exception:
            recs = []
        for it, score in recs:
            preds[it] = preds.get(it, 0.0) + weights[name] * score

    ranked = sorted(preds.items(), key=lambda x: x[1], reverse=True)
    return ranked[:top_n]

# ==================== 5) Chọn model tốt nhất ====================
def select_best_model(all_metrics, criterion="NDCG@K", task="ranking"):
    """
    Chọn model tốt nhất dựa trên 1 tiêu chí.
    - all_metrics: dict {"user_cf": {...}, "item_cf": {...}, "mf": {...}}
    - criterion: "NDCG@K", "Precision@K", "RMSE", "MAE"
    - task: "ranking" hoặc "rating"
    """
    best_model_name = None
    best_score = -np.inf if task == "ranking" else np.inf

    for name, metrics in all_metrics.items():
        score = metrics.get(criterion, 0.0)
        if task == "ranking":
            if score > best_score:
                best_score = score
                best_model_name = name
        else:  # rating
            if score < best_score:
                best_score = score
                best_model_name = name

    model_map = {"user_cf": user_cf, "item_cf": item_cf, "mf": mf}
    return best_model_name, model_map[best_model_name]

# ==================== 6) Main test ====================
if __name__ == "__main__":
    print("Loading interactions...")
    df = load_interactions()
    train_df, test_df = train_test_split(df, test_ratio=0.2)
    users = df["UserID"].unique()

    print(f"Train size: {len(train_df)}, Test size: {len(test_df)}")
    
    print("\n=== Training Matrix Factorization ===")
    mf.train(train_df, k=3, n_iter=20)

    print("\n=== Building ItemCF model ===")
    item_cf.build_model(train_df)

    # ==== Evaluate models ====
    metrics_user = evaluate_model(user_cf, train_df, test_df, task="ranking")
    metrics_user.update(evaluate_model(user_cf, train_df, test_df, task="rating"))
    metrics_item = evaluate_model(item_cf, train_df, test_df, task="ranking")
    metrics_item.update(evaluate_model(item_cf, train_df, test_df, task="rating"))
    metrics_mf = evaluate_model(mf, train_df, test_df, task="ranking")
    metrics_mf.update(evaluate_model(mf, train_df, test_df, task="rating"))

    all_metrics = {
        "user_cf": metrics_user,
        "item_cf": metrics_item,
        "mf": metrics_mf
    }

    print("\n=== Individual model metrics ===")
    for name, metrics in all_metrics.items():
        print(f"{name}: {metrics}")

    # ==== Chọn model tốt nhất ====
    best_model_name, best_model = select_best_model(all_metrics, criterion="NDCG@K", task="ranking")
    print(f"\nBest model for ranking: {best_model_name}")

    # ==== Top-10 theo model tốt nhất ====
    print("\n=== Top-10 recommendations per user using best model ===")
    for u in users:
        recs = best_model.recommend_top_n(u, top_n=10)
        print(f"User {u}: {[it for it, _ in recs]}")
