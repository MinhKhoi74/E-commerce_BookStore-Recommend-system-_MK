"""
matrix_factorization.py
- Matrix Factorization (SGD)
- Explicit = Rating, Implicit = Score (kết hợp)
- Viết lại để dùng cho evaluate
"""

import numpy as np
import pandas as pd
from load_data import load_interactions

# ======== Globals ========
all_users = []
all_items = []
P = None
Q = None
rating_df = None   # để biết user đã rated gì

# ======== 1) Train ========
def train(train_df: pd.DataFrame | None = None, k=3, alpha=0.01, lam=0.05, n_iter=30):
    """
    train_df: nếu None sẽ load từ load_interactions()
    Trả về R_df để kiểm tra / evaluate
    """
    global all_users, all_items, P, Q, rating_df

    if train_df is None:
        df = load_interactions()
    else:
        df = train_df.copy()

    rating_df = df.dropna(subset=['Rating']).copy()

    all_users = sorted(df['UserID'].unique())
    all_items = sorted(df['BookID'].unique())

    # combine: ưu tiên Rating nếu có, ngược lại Score
    df['Combined'] = df['Rating'].fillna(0) + df['Score'].fillna(0)

    R_df = df.pivot(index='UserID', columns='BookID', values='Combined') \
             .reindex(index=all_users, columns=all_items).fillna(0)

    R = R_df.to_numpy()
    num_users, num_items = R.shape

    # Khởi tạo latent factors
    P = np.random.normal(scale=1./k, size=(num_users, k))
    Q = np.random.normal(scale=1./k, size=(num_items, k))

    # SGD
    for it in range(n_iter):
        for u in range(num_users):
            for i in range(num_items):
                if R[u, i] == 0: continue
                err = R[u, i] - np.dot(P[u], Q[i])
                P[u] += alpha * (err * Q[i] - lam * P[u])
                Q[i] += alpha * (err * P[u] - lam * Q[i])

    return R_df   # để evaluate nếu cần

# ======== 2) Prediction ========
def normalize_pred(pred: float | None) -> float | None:
    if pred is None: return None
    p = max(-10.0, min(10.0, pred))
    return (p + 10.0) / 20.0   # scale về [0,1]

def predict_rating(user, item):
    global all_users, all_items, P, Q
    if user not in all_users or item not in all_items: 
        return None
    u = all_users.index(user)
    i = all_items.index(item)
    return normalize_pred(np.dot(P[u], Q[i]))

# ======== 3) Recommend top-N ========
def recommend_top_n(user, top_n=3):
    global all_users, all_items, P, Q, rating_df
    if user not in all_users: 
        return []
    u = all_users.index(user)
    scores = P[u] @ Q.T
    rated_items = set(rating_df[rating_df['UserID'] == user]['BookID'])
    preds = [(it, normalize_pred(scores[i])) for i, it in enumerate(all_items) if it not in rated_items]
    preds = [(it, p) for it, p in preds if p is not None]
    preds = sorted(preds, key=lambda x: x[1], reverse=True)
    return preds[:top_n]

