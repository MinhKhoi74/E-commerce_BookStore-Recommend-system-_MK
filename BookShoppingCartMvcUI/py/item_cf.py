"""
Dual-model ItemCF example (Pearson for explicit ratings, Cosine for implicit feedback)
- Item-based: transpose user-item -> item-user
- build_model(train_df) để dùng cho evaluate.py
"""

import pandas as pd
import numpy as np

np.set_printoptions(suppress=True, precision=6)

# ======== GLOBAL STATE ========
rating_matrix = None
implicit_matrix = None
rating_item_matrix = None
implicit_item_matrix = None
rating_sims = None
implicit_sims = None


# ======== 1) Similarity functions ========
def pearson_similarity_rows(row1: pd.Series, row2: pd.Series):
    mask = ~row1.isna() & ~row2.isna()
    if mask.sum() < 2:
        return 0.0
    mx = float(row1.dropna().mean())
    my = float(row2.dropna().mean())
    x = row1[mask].to_numpy(dtype=float) - mx
    y = row2[mask].to_numpy(dtype=float) - my
    num = float(np.sum(x * y))
    den = float(np.sqrt(np.sum(x**2)) * np.sqrt(np.sum(y**2)))
    return 0.0 if den == 0 else num / den


def cosine_similarity_full(vecs_df: pd.DataFrame):
    M = vecs_df.fillna(0).to_numpy(dtype=float)
    norms = np.linalg.norm(M, axis=1)
    sim = np.zeros((M.shape[0], M.shape[0]), dtype=float)
    for i in range(M.shape[0]):
        for j in range(M.shape[0]):
            if norms[i] == 0 or norms[j] == 0:
                sim[i, j] = 0.0
            else:
                sim[i, j] = float(np.dot(M[i], M[j]) / (norms[i] * norms[j]))
    return pd.DataFrame(sim, index=vecs_df.index, columns=vecs_df.index)


# ======== 2) Build model from train_df ========
def build_model(train_df: pd.DataFrame):
    global rating_matrix, implicit_matrix, rating_item_matrix, implicit_item_matrix, rating_sims, implicit_sims

    rating_df = train_df.dropna(subset=['Rating']).copy()
    implicit_df = train_df.dropna(subset=['Score']).copy()

    all_users = sorted(train_df['UserID'].unique())
    all_items = sorted(train_df['BookID'].unique())

    rating_matrix = rating_df.pivot(index='UserID', columns='BookID', values='Score') \
                             .reindex(index=all_users, columns=all_items)
    implicit_matrix = implicit_df.pivot(index='UserID', columns='BookID', values='Rating') \
                                 .reindex(index=all_users, columns=all_items)

    # chuyển sang item-based
    rating_item_matrix = rating_matrix.T
    implicit_item_matrix = implicit_matrix.T

    # similarity
    rating_sims = pd.DataFrame(index=rating_item_matrix.index, columns=rating_item_matrix.index, dtype=float)
    for i1 in rating_item_matrix.index:
        for i2 in rating_item_matrix.index:
            rating_sims.loc[i1, i2] = pearson_similarity_rows(rating_item_matrix.loc[i1], rating_item_matrix.loc[i2])

    implicit_sims = cosine_similarity_full(implicit_item_matrix)


# ======== 3) Prediction functions ========
def predict_rating(user: str, item: str, k: int | None = None) -> float | None:
    global rating_matrix, rating_item_matrix, rating_sims
    if user not in rating_matrix.index:
        return None

    # fallback nếu item chưa xuất hiện
    if item not in rating_item_matrix.index:
        user_mean = rating_matrix.loc[user].dropna().mean()
        return float(user_mean) if not np.isnan(user_mean) else 0.0

    if item in rating_matrix.columns and not pd.isna(rating_matrix.loc[user, item]):
        return float(rating_matrix.loc[user, item])

    neighbors = []
    for j in rating_matrix.columns:
        if j == item or pd.isna(rating_matrix.loc[user, j]):
            continue
        if j not in rating_item_matrix.index:
            continue
        sim = float(rating_sims.loc[item, j])
        if sim == 0.0:
            continue
        item_mean = float(rating_item_matrix.loc[item].dropna().mean()) if not rating_item_matrix.loc[item].dropna().empty else 0.0
        j_mean = float(rating_item_matrix.loc[j].dropna().mean()) if not rating_item_matrix.loc[j].dropna().empty else 0.0
        r_uj = float(rating_matrix.loc[user, j])
        contrib = sim * (r_uj - j_mean)
        neighbors.append((j, sim, r_uj, j_mean, contrib, item_mean))

    if not neighbors:
        return float(rating_matrix.loc[user].dropna().mean() or 0.0)

    if k and len(neighbors) > k:
        neighbors = sorted(neighbors, key=lambda x: abs(x[1]), reverse=True)[:k]

    num = sum(c for *_, c, _ in neighbors)
    den = sum(abs(s) for _, s, *_ in neighbors)
    item_mean = neighbors[0][-1] if neighbors else 0.0
    pred = item_mean + (num / den if den != 0 else 0.0)
    return pred


def predict_implicit(user: str, item: str, k: int | None = None) -> float | None:
    global implicit_matrix, implicit_item_matrix, implicit_sims
    if user not in implicit_matrix.index:
        return None

    if item not in implicit_item_matrix.index:
        return float(implicit_matrix.loc[user].dropna().mean() or 0.0)

    if item in implicit_matrix.columns and not pd.isna(implicit_matrix.loc[user, item]):
        return float(implicit_matrix.loc[user, item])

    neighbors = []
    for j in implicit_matrix.columns:
        if j == item or pd.isna(implicit_matrix.loc[user, j]):
            continue
        if j not in implicit_item_matrix.index:
            continue
        sim = float(implicit_sims.loc[item, j])
        if sim == 0.0:
            continue
        score = float(implicit_matrix.loc[user, j])
        neighbors.append((j, sim, score))

    if not neighbors:
        return float(implicit_matrix.loc[user].dropna().mean() or 0.0)

    if k and len(neighbors) > k:
        neighbors = sorted(neighbors, key=lambda x: x[1], reverse=True)[:k]

    num = sum(sim * score for _, sim, score in neighbors)
    den = sum(abs(sim) for _, sim, _ in neighbors)
    return (num / den) if den != 0 else float(implicit_matrix.loc[user].dropna().mean() or 0.0)


# ======== 4) Normalization + ensemble ========
def normalize_rating_pred(pred: float | None) -> float | None:
    if pred is None:
        return None
    p = max(-10.0, min(10.0, pred))
    return (p + 10.0) / 20.0


def normalize_implicit_pred(pred: float | None) -> float | None:
    if pred is None:
        return None
    p = max(0.0, min(5.0, pred))
    return p / 5.0


def ensemble_predict(user: str, item: str, alpha: float = 0.6, k: int | None = None):
    r_pred = predict_rating(user, item, k=k)
    i_pred = predict_implicit(user, item, k=k)

    nr = normalize_rating_pred(r_pred)
    ni = normalize_implicit_pred(i_pred)

    if nr is not None and ni is not None:
        return alpha * nr + (1 - alpha) * ni
    if nr is not None:
        return nr
    if ni is not None:
        return ni
    return None


# ======== 5) Recommend top-N ========
def recommend_top_n(user: str, top_n: int = 3, alpha: float = 0.6):
    global rating_matrix, implicit_matrix
    items = sorted(set(list(rating_matrix.columns) + list(implicit_matrix.columns)))
    preds = {}
    for it in items:
        if it in rating_matrix.columns and not pd.isna(rating_matrix.loc[user, it]):
            continue
        p = ensemble_predict(user, it, alpha=alpha)
        if p is None:
            continue
        preds[it] = p
    ranked = sorted(preds.items(), key=lambda x: x[1], reverse=True)
    return ranked[:top_n]
