"""
Dual-model UserCF example (Pearson for explicit ratings, Cosine for implicit feedback)
Corrected:
 - Pearson uses each user's global mean (over their rated items) when computing correlation.
 - predict_rating falls back to user mean even if item not present in rating matrix.
 - recommend_top_n only excludes items the user has explicit rating (implicit events do not exclude).
"""

import pandas as pd
import numpy as np
from load_data import load_interactions   # <<=== lấy data từ file ngoài

np.set_printoptions(suppress=True, precision=6)

def print_title(t):
    print('\n' + '='*10 + ' ' + t + ' ' + '='*10)

def print_sub(t):
    print('\n' + '-'*6 + ' ' + t + ' ' + '-'*6)


# ======== 1) Load data từ SQL qua load_data.py ========
df = load_interactions()
print_title('BƯỚC 1: DỮ LIỆU GỐC TỪ DB')
print(df.head())

# ======== 2) Split explicit vs implicit ========
rating_df = df.dropna(subset=['Rating']).copy()    # explicit rating
implicit_df = df.dropna(subset=['Score']).copy() # implicit feedback

print_title('BƯỚC 2: RATING (explicit)')
print(rating_df)
print_title('BƯỚC 3: IMPLICIT (behavior)')
print(implicit_df)

# ======== 3) Pivot -> matrices ========
all_users = sorted(df['UserID'].unique())
all_items = sorted(df['BookID'].unique())

rating_matrix = rating_df.pivot(index='UserID', columns='BookID', values='Score') \
                         .reindex(index=all_users, columns=all_items)
implicit_matrix = implicit_df.pivot(index='UserID', columns='BookID', values='Rating') \
                             .reindex(index=all_users, columns=all_items)

print_title('MA TRẬN RATING (NaN = no explicit rating)')
print(rating_matrix)
print_title('MA TRẬN IMPLICIT (NaN = no behavior event)')
print(implicit_matrix)


# ======== 4) Similarity functions ========
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
    return 0.0 if den == 0 else num/den

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


# ======== 5) Build similarity matrices ========
print_title('BƯỚC 4: TÍNH MA TRẬN TƯƠNG ĐỒNG')
rating_sims = pd.DataFrame(index=rating_matrix.index, columns=rating_matrix.index, dtype=float)
for u1 in rating_matrix.index:
    for u2 in rating_matrix.index:
        rating_sims.loc[u1, u2] = pearson_similarity_rows(rating_matrix.loc[u1], rating_matrix.loc[u2])
print_sub('Pearson similarity (rating)')
print(rating_sims)

implicit_sims = cosine_similarity_full(implicit_matrix)
print_sub('Cosine similarity (implicit, full-vector)')
print(implicit_sims)


# ======== 6) Prediction functions ========
def predict_rating(user: str, item: str, k: int | None = None) -> float | None:
    if user not in rating_matrix.index:
        return None
    if item in rating_matrix.columns and not pd.isna(rating_matrix.loc[user, item]):
        return float(rating_matrix.loc[user, item])

    user_ratings = rating_matrix.loc[user].dropna()
    user_mean = float(user_ratings.mean()) if not user_ratings.empty else None

    neighbors = []
    for other in rating_matrix.index:
        if other == user:
            continue
        if item not in rating_matrix.columns or pd.isna(rating_matrix.loc[other, item]):
            continue
        sim = float(rating_sims.loc[user, other])
        if sim == 0.0:
            continue
        other_mean = float(rating_matrix.loc[other].dropna().mean()) if not rating_matrix.loc[other].dropna().empty else 0.0
        r_vi = float(rating_matrix.loc[other, item])
        contrib = sim * (r_vi - other_mean)
        neighbors.append((other, sim, r_vi, other_mean, contrib))

    if not neighbors:
        return user_mean

    if k is not None and k > 0 and len(neighbors) > k:
        neighbors = sorted(neighbors, key=lambda x: abs(x[1]), reverse=True)[:k]

    num = sum(c for *_, c in neighbors)
    den = sum(abs(s) for _, s, *_ in neighbors)
    pred = user_mean + (num / den if den != 0 else 0.0) if user_mean is not None else None
    return pred

def predict_implicit(user: str, item: str, k: int | None = None) -> float | None:
    if user not in implicit_matrix.index:
        return None
    if item in implicit_matrix.columns and not pd.isna(implicit_matrix.loc[user, item]):
        return float(implicit_matrix.loc[user, item])

    neighbors = []
    for other in implicit_matrix.index:
        if other == user:
            continue
        if item not in implicit_matrix.columns or pd.isna(implicit_matrix.loc[other, item]):
            continue
        sim = float(implicit_sims.loc[user, other])
        if sim == 0.0:
            continue
        neighbors.append((other, sim, float(implicit_matrix.loc[other, item])))

    if not neighbors:
        return None

    if k is not None and k > 0 and len(neighbors) > k:
        neighbors = sorted(neighbors, key=lambda x: x[1], reverse=True)[:k]

    num = sum(sim * score for _, sim, score in neighbors)
    den = sum(abs(sim) for _, sim, _ in neighbors)
    return (num / den) if den != 0 else None


# ======== 7) Normalization + ensemble ========
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


# ======== 8) Recommend top-N ========
def recommend_top_n(user: str, top_n: int = 3, alpha: float = 0.6):
    items = sorted(set(list(rating_matrix.columns) + list(implicit_matrix.columns)))
    preds = {}
    for it in items:
        has_rating = it in rating_matrix.columns and not pd.isna(rating_matrix.loc[user, it])
        if has_rating:
            continue
        p = ensemble_predict(user, it, alpha=alpha, k=None)
        if p is None:
            continue
        preds[it] = p
    ranked = sorted(preds.items(), key=lambda x: x[1], reverse=True)
    return ranked[:top_n]


