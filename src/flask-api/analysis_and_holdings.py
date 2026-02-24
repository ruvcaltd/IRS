import yfinance as yf
import pandas as pd

def safe_to_text(name, obj):
    """Convert any yfinance object (DataFrame / Series /dict / scalar / None) to readable text."""
    if obj is None:
        return f"=== {name} ===\nNone\n\n"

    # DataFrame or Series: use tabular text
    if isinstance(obj, (pd.DataFrame, pd.Series)):
        text = obj.to_string()
    else:
        # dict, list, scalar, etc.
        text = repr(obj)
    return f"=== {name} ===\n{text}\n\n"


def _get_analyst_price_target(ticker) -> dict:
    """Return analyst price target as a dict with keys 'avg', 'low', 'high'.

    Preferred source: ticker.info (targetMeanPrice/targetLowPrice/targetHighPrice).
    Backwards-compatible fallbacks: ticker.analyst_price_target or
    ticker.analyst_price_targets (legacy yfinance attributes).
    Always returns a dict (empty if no data).
    """
    # Try ticker.info first (most reliable across yfinance versions)
    try:
        info = getattr(ticker, "info", {}) or {}
    except Exception:
        info = {}

    mean = info.get("targetMeanPrice")
    low = info.get("targetLowPrice")
    high = info.get("targetHighPrice")
    if any(v is not None for v in (mean, low, high)):
        return {"avg": mean, "low": low, "high": high}

    # Fallback: legacy attribute(s)
    for attr in ("analyst_price_target", "analyst_price_targets"):
        if hasattr(ticker, attr):
            try:
                val = getattr(ticker, attr)
                if isinstance(val, dict):
                    # normalize keys if possible
                    if "avg" in val or "mean" in val or "targetMeanPrice" in val:
                        # try to normalize to avg/low/high keys
                        avg = val.get("avg") or val.get("mean") or val.get("targetMeanPrice")
                        low_v = val.get("low") or val.get("targetLowPrice")
                        high_v = val.get("high") or val.get("targetHighPrice")
                        return {"avg": avg, "low": low_v, "high": high_v}
                    return val
            except Exception:
                pass

    # Nothing found
    return {}


# --- Compatibility helpers for removed/renamed yfinance properties ---

def _try_attrs(obj, *names):
    """Return first existent attribute value on obj from names or None."""
    for n in names:
        if hasattr(obj, n):
            try:
                return getattr(obj, n)
            except Exception:
                continue
    return None


def _get_earnings_estimates(ticker):
    """Return earnings estimates DataFrame (handles singular/plural renames)."""
    # prefer singular/current yfinance name first
    val = _try_attrs(ticker, 'earnings_estimate', 'earnings_estimates')
    if val is not None:
        return val
    # try base API method
    try:
        if hasattr(ticker, 'get_earnings_estimate'):
            return ticker.get_earnings_estimate()
    except Exception:
        pass
    return pd.DataFrame()


def _get_revenue_estimates(ticker):
    """Return revenue estimates DataFrame (handles singular/plural renames)."""
    val = _try_attrs(ticker, 'revenue_estimate', 'revenue_estimates')
    if val is not None:
        return val
    try:
        if hasattr(ticker, 'get_revenue_estimate'):
            return ticker.get_revenue_estimate()
    except Exception:
        pass
    return pd.DataFrame()


def _get_eps_revisions_summary(ticker):
    """Return EPS revisions summary (fall back to eps_revisions DataFrame if summary missing)."""
    # prefer detailed eps_revisions table (current API) then legacy summary
    val2 = _try_attrs(ticker, 'eps_revisions', 'eps_revisions_summary')
    if val2 is not None:
        return val2
    try:
        if hasattr(ticker, 'get_eps_revisions'):
            return ticker.get_eps_revisions()
    except Exception:
        pass
    return pd.DataFrame()


def _get_fund_holders(ticker):
    """Return fund holders table (maps old 'fund_holders' to 'mutualfund_holders')."""
    val = _try_attrs(ticker, 'mutualfund_holders', 'fund_holders')
    if val is not None:
        return val
    try:
        if hasattr(ticker, 'get_mutualfund_holders'):
            return ticker.get_mutualfund_holders()
    except Exception:
        pass
    return pd.DataFrame()


def _get_insider_holders(ticker):
    """Return insider holders (maps old 'insider_holders' to 'insider_roster' / 'insider_roster_holders')."""
    val = _try_attrs(ticker, 'insider_roster', 'insider_roster_holders', 'insider_holders')
    if val is not None:
        return val
    try:
        if hasattr(ticker, 'get_insider_roster_holders'):
            return ticker.get_insider_roster_holders()
    except Exception:
        pass
    # last-resort: return insider_purchases/transactions
    val2 = _try_attrs(ticker, 'insider_purchases', 'insider_transactions')
    if val2 is not None:
        return val2
    return pd.DataFrame()


def _get_net_share_purchase_activity(ticker):
    """Return net share purchase activity (map to insider_purchases where available)."""
    val = _try_attrs(ticker, 'insider_purchases', 'net_share_purchase_activity')
    if val is not None:
        return val
    try:
        if hasattr(ticker, 'get_insider_purchases'):
            return ticker.get_insider_purchases()
    except Exception:
        pass
    return pd.DataFrame()


def get_full_analysis_and_holdings_text(ticker: str) -> str:
    """
    Fetch all Analysis & Holdings information exposed on:
    https://ranaroussi.github.io/yfinance/reference/yfinance.analysis.html
    and return as a single text string for LLM consumption.
    """

    t = yf.Ticker(ticker)

    sections = []

    # --- Analysis section (all documented methods) ---
    # The page documents methods like these, each with an `as_dict` option. [page:0]
    # You can add/remove methods here as the API evolves.

    # Recommendations summary (strongBuy, buy, hold, sell, strongSell)
    try:
        sections.append(safe_to_text("analysis.recommendations", t.recommendations))
    except Exception as e:
        sections.append(f"=== analysis.recommendations ===\nERROR: {e}\n\n")

    # Recommendations with changes (upgrades/downgrades)
    try:
        sections.append(safe_to_text("analysis.recommendations_summary", t.recommendations_summary))
    except Exception as e:
        sections.append(f"=== analysis.recommendations_summary ===\nERROR: {e}\n\n")

    # Price target (current, low, high, mean, median)
    try:
        pt = _get_analyst_price_target(t)
        sections.append(safe_to_text("analysis.analyst_price_target", pt))
    except Exception as e:
        sections.append(f"=== analysis.analyst_price_target ===\nERROR: {e}\n\n")

    # Earnings estimates (EPS) by quarter/year
    try:
        sections.append(safe_to_text("analysis.earnings_estimates", _get_earnings_estimates(t)))
    except Exception as e:
        sections.append(f"=== analysis.earnings_estimates ===\nERROR: {e}\n\n")

    # Revenue estimates by quarter/year
    try:
        sections.append(safe_to_text("analysis.revenue_estimates", _get_revenue_estimates(t)))
    except Exception as e:
        sections.append(f"=== analysis.revenue_estimates ===\nERROR: {e}\n\n")

    # EPS history (estimate vs actual, surprise, etc.)
    try:
        sections.append(safe_to_text("analysis.eps_trend", t.eps_trend))
    except Exception as e:
        sections.append(f"=== analysis.eps_trend ===\nERROR: {e}\n\n")

    # EPS revision (current vs 7, 30, 60, 90 days ago)
    try:
        sections.append(safe_to_text("analysis.eps_revisions", t.eps_revisions))
    except Exception as e:
        sections.append(f"=== analysis.eps_revisions ===\nERROR: {e}\n\n")

    # EPS revision summary (up/down last 7/30 days)
    try:
        sections.append(safe_to_text("analysis.eps_revisions_summary", _get_eps_revisions_summary(t)))
    except Exception as e:
        sections.append(f"=== analysis.eps_revisions_summary ===\nERROR: {e}\n\n")

    # Growth estimates (stock/industry/sector/index over 0q, +1q, 0y, +1y, +5y, -5y) [page:0]
    try:
        sections.append(safe_to_text("analysis.growth_estimates", t.growth_estimates))
    except Exception as e:
        sections.append(f"=== analysis.growth_estimates ===\nERROR: {e}\n\n")

    # --- Holdings section (all documented methods) --- [page:0]

    # Main institutional holders table
    try:
        sections.append(safe_to_text("holdings.institutional_holders", t.institutional_holders))
    except Exception as e:
        sections.append(f"=== holdings.institutional_holders ===\nERROR: {e}\n\n")

    # Major holders (top owners breakdown)
    try:
        sections.append(safe_to_text("holdings.major_holders", t.major_holders))
    except Exception as e:
        sections.append(f"=== holdings.major_holders ===\nERROR: {e}\n\n")

    # Fund holders
    try:
        sections.append(safe_to_text("holdings.fund_holders", _get_fund_holders(t)))
    except Exception as e:
        sections.append(f"=== holdings.fund_holders ===\nERROR: {e}\n\n")

    # Insider holders
    try:
        sections.append(safe_to_text("holdings.insider_holders", _get_insider_holders(t)))
    except Exception as e:
        sections.append(f"=== holdings.insider_holders ===\nERROR: {e}\n\n")

    # Insider transactions
    try:
        sections.append(safe_to_text("holdings.insider_transactions", t.insider_transactions))
    except Exception as e:
        sections.append(f"=== holdings.insider_transactions ===\nERROR: {e}\n\n")

    # Net share purchase activity
    try:
        sections.append(safe_to_text("holdings.net_share_purchase_activity", _get_net_share_purchase_activity(t)))
    except Exception as e:
        sections.append(f"=== holdings.net_share_purchase_activity ===\nERROR: {e}\n\n")

    # Combine everything into a single text block
    return "".join(sections)
