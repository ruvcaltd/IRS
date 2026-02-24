from flask import Blueprint, request, jsonify, current_app
from flask_smorest import Blueprint as SmorestBlueprint
import os
import sys
import datetime
import jwt
import requests
import yfinance as yf
from analysis_and_holdings import get_full_analysis_and_holdings_text

# create a smorest blueprint so that swagger UI pick up descriptions
finance_bp = SmorestBlueprint(
    'finance', __name__, url_prefix='', description='Yahoo Finance endpoints'
)

# helper utilities -----------------------------------------------------------

def _safe_get(info: dict, key: str, default="N/A"):
    v = info.get(key)
    return default if v is None else v


def _fmt_num(v, decimals=2, prefix="", suffix=""):
    if v is None or v == "N/A":
        return "N/A"
    try:
        n = float(v)
        if abs(n) >= 1e12:
            return f"{prefix}{n/1e12:.{decimals}f}T{suffix}"
        if abs(n) >= 1e9:
            return f"{prefix}{n/1e9:.{decimals}f}B{suffix}"
        if abs(n) >= 1e6:
            return f"{prefix}{n/1e6:.{decimals}f}M{suffix}"
        if abs(n) >= 1e3:
            return f"{prefix}{n/1e3:.{decimals}f}K{suffix}"
        return f"{prefix}{n:.{decimals}f}{suffix}"
    except (ValueError, TypeError):
        return str(v)


def _fmt_pct(v, decimals=2):
    if v is None or v == "N/A":
        return "N/A"
    try:
        return f"{float(v)*100:.{decimals}f}%" if abs(float(v)) < 1 else f"{float(v):.{decimals}f}%"
    except (ValueError, TypeError):
        return str(v)


def _color_change(v):
    if v is None or v == "N/A":
        return "N/A"
    try:
        n = float(v)
        color = "green" if n >= 0 else "red"
        sign = "+" if n >= 0 else ""
        return f"[{color}]{sign}{n:.2f}%[/{color}]"
    except (ValueError, TypeError):
        return str(v)


def _get_ticker(symbol: str):
    t = yf.Ticker(symbol)
    info = t.info
    if not info or (info.get("regularMarketPrice") is None and info.get("previousClose") is None):
        if info.get("symbol") is None:
            raise ValueError(f"Ticker '{symbol}' not found")
    return t, info


# authentication decorator --------------------------------------------------

def token_required(f):
    from functools import wraps

    @wraps(f)
    def decorated(*args, **kwargs):
        token = request.headers.get('Authorization')
        if not token:
            return jsonify({'error': 'Token missing'}), 401
        try:
            token = token.split(' ')[1]  # Bearer token
            jwt.decode(token, current_app.secret_key, algorithms=['HS256'])
        except jwt.ExpiredSignatureError:
            return jsonify({'error': 'Token expired'}), 401
        except Exception:
            return jsonify({'error': 'Invalid token'}), 401
        return f(*args, **kwargs)

    return decorated


# startup environment warning (does not stop application)
@finance_bp.before_app_request
def check_env():
    # this will run before the first request for any blueprint route
    required_vars = ['Yahoo_Fin_user', 'Yahoo_fin_secret', 'SECRET_KEY', 'BRAVE_API_TOKEN', 'BRAVE_GOGGLES_URL']
    missing = [var for var in required_vars if not os.getenv(var)]
    if missing:
        print(f"[warning] Missing optional environment variables: {', '.join(missing)}", file=sys.stderr)
        print("Some endpoints may fail if credentials are not provided.", file=sys.stderr)


# command logic -------------------------------------------------------------

def cmd_price(symbol: str) -> dict:
    symbol = symbol.upper()
    t, info = _get_ticker(symbol)

    price = _safe_get(info, "regularMarketPrice", _safe_get(info, "previousClose"))
    prev = _safe_get(info, "regularMarketPreviousClose", _safe_get(info, "previousClose"))
    change = change_pct = "N/A"
    if price != "N/A" and prev != "N/A":
        try:
            change = float(price) - float(prev)
            change_pct = (change / float(prev)) * 100
        except (ValueError, TypeError, ZeroDivisionError):
            pass

    volume = _safe_get(info, "regularMarketVolume", _safe_get(info, "volume"))
    currency = _safe_get(info, "currency", "")
    name = _safe_get(info, "shortName", symbol)

    return {"symbol": symbol, "name": name, "price": price, "change": change, "changePct": change_pct, "volume": volume, "currency": currency}



def cmd_quote(symbol: str) -> dict:
    symbol = symbol.upper()
    t, info = _get_ticker(symbol)

    fields = [
        ("Name", _safe_get(info, "shortName")),
        ("Price", f"{_safe_get(info, 'regularMarketPrice')} {_safe_get(info, 'currency', '')}"),
        ("Previous Close", _safe_get(info, "previousClose")),
        ("Open", _safe_get(info, "regularMarketOpen", _safe_get(info, "open"))),
        ("Day Range", f"{_safe_get(info, 'regularMarketDayLow', _safe_get(info, 'dayLow'))} - {_safe_get(info, 'regularMarketDayHigh', _safe_get(info, 'dayHigh'))}"),
        ("52W Range", f"{_safe_get(info, 'fiftyTwoWeekLow')} - {_safe_get(info, 'fiftyTwoWeekHigh')}"),
        ("Volume", _fmt_num(_safe_get(info, "regularMarketVolume"), 0)),
        ("Avg Volume", _fmt_num(_safe_get(info, "averageVolume"), 0)),
        ("Market Cap", _fmt_num(_safe_get(info, "marketCap"))),
        ("P/E (TTM)", _safe_get(info, "trailingPE")),
        ("P/E (Fwd)", _safe_get(info, "forwardPE")),
        ("EPS (TTM)", _safe_get(info, "trailingEps")),
        ("Div Yield", _fmt_pct(_safe_get(info, "dividendYield"))),
        ("Beta", _safe_get(info, "beta")),
        ("Sector", _safe_get(info, "sector")),
        ("Industry", _safe_get(info, "industry")),
    ]

    return {k: v for k, v in fields}


def cmd_compare(symbols: list[str]) -> dict:
    if len(symbols) < 2:
        raise ValueError("Provide at least 2 tickers")

    data = {}
    for s in symbols:
        try:
            t, info = _get_ticker(s)
            data[s] = info
        except ValueError:
            data[s] = {}

    metrics = [
        ("Price", "regularMarketPrice"),
        ("Change %", None),  # computed
        ("Market Cap", "marketCap"),
        ("P/E (TTM)", "trailingPE"),
        ("P/E (Fwd)", "forwardPE"),
        ("Div Yield", "dividendYield"),
        ("Beta", "beta"),
        ("52W Low", "fiftyTwoWeekLow"),
        ("52W High", "fiftyTwoWeekHigh"),
        ("Volume", "regularMarketVolume"),
    ]

    out = {}
    for s in symbols:
        info = data.get(s, {})
        out[s] = {label: _safe_get(info, key) if key else "N/A" for label, key in metrics}

    return out


def cmd_credit(symbol: str) -> dict:
    symbol = symbol.upper()
    t, info = _get_ticker(symbol)

    bs = t.balance_sheet
    fin = t.financials
    cf = t.cashflow

    result = {"symbol": symbol, "name": _safe_get(info, "shortName", symbol)}

    # Extract latest period data
    def _latest(df, row_names):
        if df is None or df.empty:
            return None
        for name in row_names:
            if name in df.index:
                val = df.iloc[:, 0].get(name)  # latest column
                if val is not None:
                    try:
                        return float(val)
                    except (ValueError, TypeError):
                        pass
        return None

    total_debt = _latest(bs, ["Total Debt", "Long Term Debt And Capital Lease Obligation", "Long Term Debt", "Total Non Current Liabilities Net Minority Interest"])
    short_debt = _latest(bs, ["Current Debt", "Current Debt And Capital Lease Obligation", "Current Portion Of Long Term Debt"])
    long_debt = _latest(bs, ["Long Term Debt", "Long Term Debt And Capital Lease Obligation"])
    total_assets = _latest(bs, ["Total Assets"])
    total_equity = _latest(bs, ["Total Equity Gross Minority Interest", "Stockholders Equity", "Common Stock Equity"])
    cash = _latest(bs, ["Cash And Cash Equivalents", "Cash Cash Equivalents And Short Term Investments", "Cash Financial"])

    ebitda = _latest(fin, ["EBITDA", "Normalized EBITDA"])
    ebit = _latest(fin, ["EBIT", "Operating Income"])
    interest_expense = _latest(fin, ["Interest Expense", "Interest Expense Non Operating", "Net Interest Income"])
    revenue = _latest(fin, ["Total Revenue"])
    net_income = _latest(fin, ["Net Income", "Net Income Common Stockholders"])

    # Compute ratios
    def _ratio(num, den):
        if num is not None and den is not None and den != 0:
            return num / den
        return None

    net_debt = None
    if total_debt is not None and cash is not None:
        net_debt = total_debt - cash

    ratios = {
        "Total Debt": total_debt,
        "Short-term Debt": short_debt,
        "Long-term Debt": long_debt,
        "Cash & Equivalents": cash,
        "Net Debt": net_debt,
        "Total Assets": total_assets,
        "Total Equity": total_equity,
        "EBITDA": ebitda,
        "EBIT": ebit,
        "Interest Expense": interest_expense,
        "Revenue": revenue,
        "Net Income": net_income,
        "Debt/Equity": _ratio(total_debt, total_equity),
        "Debt/Assets": _ratio(total_debt, total_assets),
        "Debt/EBITDA": _ratio(total_debt, ebitda),
        "Net Debt/EBITDA": _ratio(net_debt, ebitda),
        "Interest Coverage (EBITDA)": _ratio(ebitda, abs(interest_expense) if interest_expense else None),
        "Interest Coverage (EBIT)": _ratio(ebit, abs(interest_expense) if interest_expense else None),
    }
    result["metrics"] = {k: v for k, v in ratios.items()}

    return result


def cmd_macro(tickers: list[str]) -> dict:
    if not tickers:
        raise ValueError("Provide tickers")

    data = {}
    for symbol in tickers:
        try:
            t = yf.Ticker(symbol)
            info = t.info
            price = info.get("regularMarketPrice", info.get("previousClose"))
            prev = info.get("regularMarketPreviousClose", info.get("previousClose"))
            change_pct = None
            if price and prev:
                change_pct = ((float(price) - float(prev)) / float(prev)) * 100
            data[symbol] = {"price": price, "changePct": change_pct}
        except Exception as e:
            data[symbol] = {"price": None, "changePct": None, "error": str(e)}

    out = {}
    for sym in tickers:
        out[sym] = {"symbol": sym, **data.get(sym, {})}
    return out


def cmd_fx(base: str = "USD") -> dict:
    pairs = {
        f"{base}/ARS": f"{base}ARS=X",
        f"{base}/BRL": f"{base}BRL=X",
        f"{base}/CLP": f"{base}CLP=X",
        f"{base}/MXN": f"{base}MXN=X",
        f"{base}/COP": f"{base}COP=X",
        f"{base}/UYU": f"{base}UYU=X",
        f"{base}/PEN": f"{base}PEN=X",
    }

    data = {}
    for name, sym in pairs.items():
        try:
            t = yf.Ticker(sym)
            info = t.info
            price = info.get("regularMarketPrice", info.get("previousClose"))
            prev = info.get("regularMarketPreviousClose", info.get("previousClose"))
            change_pct = None
            if price and prev:
                change_pct = ((float(price) - float(prev)) / float(prev)) * 100
            data[name] = {"symbol": sym, "rate": price, "changePct": change_pct}
        except Exception as e:
            data[name] = {"symbol": sym, "rate": None, "changePct": None, "error": str(e)}

    return data


def cmd_flows(symbol: str) -> dict:
    symbol = symbol.upper()
    t, info = _get_ticker(symbol)

    fund_data = {
        "Name": _safe_get(info, "shortName"),
        "Category": _safe_get(info, "category"),
        "Total Assets": _fmt_num(_safe_get(info, "totalAssets")),
        "NAV": _safe_get(info, "navPrice"),
        "Yield": _fmt_pct(_safe_get(info, "yield")),
        "YTD Return": _fmt_pct(_safe_get(info, "ytdReturn")),
        "3Y Return": _fmt_pct(_safe_get(info, "threeYearAverageReturn")),
        "5Y Return": _fmt_pct(_safe_get(info, "fiveYearAverageReturn")),
        "Expense Ratio": _fmt_pct(_safe_get(info, "annualReportExpenseRatio")),
        "Beta (3Y)": _safe_get(info, "beta3Year"),
    }

    # Top holdings
    try:
        holdings = t.funds_data.get("topHoldings", []) if hasattr(t, 'funds_data') else []
    except Exception:
        holdings = []

    # Try alternative
    if not holdings:
        try:
            # yfinance >= 0.2.36 approach
            top = t.funds_data
            if isinstance(top, dict):
                holdings = top.get("topHoldings", [])
        except Exception:
            holdings = []

    return {"fund": fund_data, "holdings": holdings}


def cmd_history(symbol: str, period: str = "1mo") -> dict:
    symbol = symbol.upper()

    valid_periods = ["1d", "5d", "1mo", "3mo", "6mo", "1y", "2y", "5y", "10y", "ytd", "max"]
    if period not in valid_periods:
        raise ValueError(f"Invalid period '{period}'. Use: {', '.join(valid_periods)}")

    t = yf.Ticker(symbol)
    hist = t.history(period=period)

    if hist.empty:
        raise ValueError(f"No history data for {symbol}")

    records = []
    for date, row in hist.iterrows():
        records.append({
            "date": str(date.date()) if hasattr(date, 'date') else str(date),
            "open": round(row.get("Open", 0), 2),
            "high": round(row.get("High", 0), 2),
            "low": round(row.get("Low", 0), 2),
            "close": round(row.get("Close", 0), 2),
            "volume": int(row.get("Volume", 0)),
        })
    return {"symbol": symbol, "period": period, "data": records}


def cmd_fundamentals(symbol: str) -> dict:
    symbol = symbol.upper()
    t, info = _get_ticker(symbol)

    statements = {}
    for name, df in [("Income Statement", t.financials), ("Balance Sheet", t.balance_sheet), ("Cash Flow", t.cashflow)]:
        if df is not None and not df.empty:
            records = {}
            for col in df.columns[:4]:  # last 4 periods
                period = str(col.date()) if hasattr(col, 'date') else str(col)[:10]
                records[period] = {str(idx): val for idx, val in df[col].items() if val is not None}
            statements[name] = records
        else:
            statements[name] = {}

    return {"symbol": symbol, "statements": statements}


def cmd_news(symbol: str) -> dict:
    symbol = symbol.upper()
    t = yf.Ticker(symbol)

    try:
        news = t.news or []
    except Exception:
        news = []

    return news[:15] if news else []


def cmd_news_summary(symbol: str, suffix: str = "Stock") -> dict:
    """Fetch news summary from Brave Search API."""
    symbol = symbol.upper()
    
    # Get environment variables
    api_token = os.getenv('BRAVE_API_TOKEN')
    goggles_url = os.getenv('BRAVE_GOGGLES_URL')
    
    if not api_token or not goggles_url:
        raise ValueError("Brave API credentials not configured")
    
    # Construct query
    query = f"{symbol} {suffix}" if suffix else symbol
    
    # Make API request
    url = "https://api.search.brave.com/res/v1/llm/context"
    headers = {
        "X-Subscription-Token": api_token
    }
    params = {
        "q": query,
        "goggles": goggles_url
    }
    
    try:
        response = requests.get(url, headers=headers, params=params, timeout=10)
        response.raise_for_status()
        data = response.json()
        return {"symbol": symbol, "query": query, "summary": data}
    except requests.exceptions.RequestException as e:
        raise ValueError(f"Brave API request failed: {str(e)}")


def cmd_search(query: str) -> dict:
    try:
        results = yf.Search(query)
        quotes = results.quotes if hasattr(results, 'quotes') else []
    except Exception as e:
        raise ValueError(f"Search error: {e}")

    return quotes[:20] if quotes else []


def cmd_options(symbol: str) -> dict:
    symbol = symbol.upper()
    t = yf.Ticker(symbol)

    try:
        dates = t.options
    except Exception:
        dates = []

    if not dates:
        return {"symbol": symbol, "expiry": None, "expirations": [], "calls": [], "puts": []}

    # Use nearest expiry
    exp = dates[0]
    chain = t.option_chain(exp)

    out = {"symbol": symbol, "expiry": exp, "expirations": list(dates)}
    out["calls"] = chain.calls.head(15).to_dict(orient="records") if chain.calls is not None else []
    out["puts"] = chain.puts.head(15).to_dict(orient="records") if chain.puts is not None else []
    return out


def cmd_dividends(symbol: str) -> dict:
    symbol = symbol.upper()
    t, info = _get_ticker(symbol)

    div_info = {
        "Dividend Rate": _safe_get(info, "dividendRate"),
        "Dividend Yield": _fmt_pct(_safe_get(info, "dividendYield")),
        "Ex-Dividend Date": _safe_get(info, "exDividendDate"),
        "Payout Ratio": _fmt_pct(_safe_get(info, "payoutRatio")),
        "5Y Avg Yield": _fmt_pct(_safe_get(info, "fiveYearAvgDividendYield")),
        "Trailing Annual Rate": _safe_get(info, "trailingAnnualDividendRate"),
        "Trailing Annual Yield": _fmt_pct(_safe_get(info, "trailingAnnualDividendYield")),
    }

    # Convert ex-div date from epoch
    ex_div = info.get("exDividendDate")
    if ex_div and isinstance(ex_div, (int, float)):
        try:
            div_info["Ex-Dividend Date"] = datetime.fromtimestamp(ex_div).strftime("%Y-%m-%d")
        except Exception:
            pass

    divs = t.dividends
    history = []
    if divs is not None and not divs.empty:
        for date, amount in divs.tail(12).items():
            history.append({
                "date": str(date.date()) if hasattr(date, 'date') else str(date)[:10],
                "amount": round(float(amount), 4)
            })

    return {"symbol": symbol, "info": div_info, "history": history}


def cmd_ratings(symbol: str) -> dict:
    symbol = symbol.upper()
    t, info = _get_ticker(symbol)

    # Recommendation summary
    rec_info = {
        "Recommendation": _safe_get(info, "recommendationKey", "").upper(),
        "Mean Rating": _safe_get(info, "recommendationMean"),
        "# of Analysts": _safe_get(info, "numberOfAnalystOpinions"),
        "Target Mean": _safe_get(info, "targetMeanPrice"),
        "Target Low": _safe_get(info, "targetLowPrice"),
        "Target High": _safe_get(info, "targetHighPrice"),
        "Target Median": _safe_get(info, "targetMedianPrice"),
    }

    # Current price for upside calc
    price = info.get("regularMarketPrice", info.get("previousClose"))
    target_mean = info.get("targetMeanPrice")
    if price and target_mean:
        try:
            upside = ((float(target_mean) - float(price)) / float(price)) * 100
            rec_info["Upside to Mean"] = f"{upside:+.1f}%"
        except (ValueError, TypeError, ZeroDivisionError):
            pass

    # Upgrades/downgrades
    upgrades = []
    try:
        ug = t.upgrades_downgrades
        if ug is not None and not ug.empty:
            for date, row in ug.tail(10).iterrows():
                upgrades.append({
                    "date": str(date.date()) if hasattr(date, 'date') else str(date)[:10],
                    "firm": row.get("Firm", ""),
                    "toGrade": row.get("ToGrade", ""),
                    "fromGrade": row.get("FromGrade", ""),
                    "action": row.get("Action", ""),
                })
    except Exception:
        pass

    return {"symbol": symbol, "summary": rec_info, "upgrades_downgrades": upgrades}

# route definitions ---------------------------------------------------------

@finance_bp.route('/login', methods=['POST'])
def login():
    """User login to obtain JWT token

    ---
    parameters:
      - name: username
        in: body
        type: string
        required: true
      - name: password
        in: body
        type: string
        required: true
    responses:
      200:
        description: JWT token returned
      401:
        description: Invalid credentials
    """
    data = request.get_json()
    if not data:
        return jsonify({'error': 'No data provided'}), 400
    user = data.get('username')
    pwd = data.get('password')
    if user == os.environ.get('Yahoo_Fin_user') and pwd == os.environ.get('Yahoo_fin_secret'):
        token = jwt.encode({'user': user, 'exp': datetime.datetime.utcnow() + datetime.timedelta(hours=1)}, current_app.secret_key, algorithm='HS256')
        return jsonify({'token': token})
    return jsonify({'error': 'Invalid credentials'}), 401


@finance_bp.route('/price/<symbol>')
@token_required
def price(symbol):
    """Retrieve price data for a given ticker symbol.

    ---
    parameters:
      - name: symbol
        in: path
        type: string
        required: true
        description: Ticker symbol (e.g. AAPL)
    responses:
      200:
        description: Price information returned as JSON
      400:
        description: Error message
    """
    try:
        return jsonify(cmd_price(symbol))
    except Exception as e:
        return jsonify({"error": str(e)}), 400


@finance_bp.route('/quote/<symbol>')
@token_required
def quote(symbol):
    """Retrieve detailed quote for a given ticker symbol.

    ---
    parameters:
      - name: symbol
        in: path
        type: string
        required: true
    responses:
      200:
        description: Quote information
      400:
        description: Error
    """
    try:
        return jsonify(cmd_quote(symbol))
    except Exception as e:
        return jsonify({"error": str(e)}), 400


@finance_bp.route('/compare')
@token_required
def compare():
    """Compare multiple ticker symbols side-by-side.

    ---
    parameters:
      - name: tickers
        in: query
        type: string
        required: true
        description: Comma-separated list of symbols (e.g. AAPL,MSFT)
    responses:
      200:
        description: Comparison data
      400:
        description: Error or invalid input
    """
    tickers = request.args.get('tickers', '').split(',')
    tickers = [s.strip().upper() for s in tickers if s.strip()]
    if len(tickers) < 2:
        return jsonify({"error": "Provide at least 2 tickers"}), 400
    try:
        return jsonify(cmd_compare(tickers))
    except Exception as e:
        return jsonify({"error": str(e)}), 400


@finance_bp.route('/credit/<symbol>')
@token_required
def credit(symbol):
    """Return credit metrics for a ticker symbol.

    ---
    parameters:
      - name: symbol
        in: path
        type: string
        required: true
    responses:
      200:
        description: Credit metrics
      400:
        description: Error
    """
    try:
        return jsonify(cmd_credit(symbol))
    except Exception as e:
        return jsonify({"error": str(e)}), 400


@finance_bp.route('/macro')
@token_required
def macro():
    """Get macro price change percentages for a list of tickers.

    ---
    parameters:
      - name: tickers
        in: query
        type: string
        required: true
        description: Comma-separated symbols
    responses:
      200:
        description: Price and change data
      400:
        description: Error
    """
    tickers = request.args.get('tickers', '').split(',')
    tickers = [s.strip().upper() for s in tickers if s.strip()]
    if not tickers:
        return jsonify({"error": "Provide tickers"}), 400
    try:
        return jsonify(cmd_macro(tickers))
    except Exception as e:
        return jsonify({"error": str(e)}), 400


@finance_bp.route('/fx')
@finance_bp.route('/fx/<base>')
@token_required
def fx(base="USD"):
    """Fetch FX rates against a base currency.

    ---
    parameters:
      - name: base
        in: path
        type: string
        required: false
        description: Base currency (default USD)
    responses:
      200:
        description: FX rates data
      400:
        description: Error
    """
    try:
        return jsonify(cmd_fx(base.upper()))
    except Exception as e:
        return jsonify({"error": str(e)}), 400


@finance_bp.route('/flows/<symbol>')
@token_required
def flows(symbol):
    """Get fund flows and holdings for an ETF/fund symbol.

    ---
    parameters:
      - name: symbol
        in: path
        type: string
        required: true
    responses:
      200:
        description: Fund data
      400:
        description: Error
    """
    try:
        return jsonify(cmd_flows(symbol))
    except Exception as e:
        return jsonify({"error": str(e)}), 400


@finance_bp.route('/history/<symbol>')
@token_required
def history(symbol):
    """Fetch historical price data for a ticker.

    ---
    parameters:
      - name: symbol
        in: path
        type: string
        required: true
      - name: period
        in: query
        type: string
        required: false
        description: Data period (e.g. 1mo, 1y)
    responses:
      200:
        description: Historical data
      400:
        description: Error
    """
    period = request.args.get('period', '1mo')
    try:
        return jsonify(cmd_history(symbol, period))
    except Exception as e:
        return jsonify({"error": str(e)}), 400


@finance_bp.route('/fundamentals/<symbol>')
@token_required
def fundamentals(symbol):
    """Retrieve financial statements for a ticker.

    ---
    parameters:
      - name: symbol
        in: path
        required: true
        type: string
    responses:
      200:
        description: Statements data
      400:
        description: Error
    """
    try:
        return jsonify(cmd_fundamentals(symbol))
    except Exception as e:
        return jsonify({"error": str(e)}), 400


@finance_bp.route('/news/<symbol>')
@token_required
def news(symbol):
    """Get recent news for a ticker.

    ---
    parameters:
      - name: symbol
        in: path
        required: true
        type: string
    responses:
      200:
        description: News list
      400:
        description: Error
    """
    try:
        return jsonify(cmd_news(symbol))
    except Exception as e:
        return jsonify({"error": str(e)}), 400


@finance_bp.route('/news_summary/<symbol>')
@token_required
def news_summary(symbol):
    """Fetch a summary of news via Brave Search API for a ticker.

    ---
    parameters:
      - name: symbol
        in: path
        required: true
        type: string
      - name: suffix
        in: query
        type: string
        required: false
        description: Suffix string appended to query (default 'Stock')
    responses:
      200:
        description: Summary data
      400:
        description: Error
    """
    suffix = request.args.get('suffix', 'Stock')
    try:
        return jsonify(cmd_news_summary(symbol, suffix))
    except Exception as e:
        return jsonify({"error": str(e)}), 400


@finance_bp.route('/search/<path:query>')
@token_required
def search(query):
    """Search for tickers matching query.

    ---
    parameters:
      - name: query
        in: path
        required: true
        type: string
    responses:
      200:
        description: Search results
      400:
        description: Error
    """
    try:
        return jsonify(cmd_search(query))
    except Exception as e:
        return jsonify({"error": str(e)}), 400


@finance_bp.route('/options/<symbol>')
@token_required
def options(symbol):
    """Get options chain for a ticker.

    ---
    parameters:
      - name: symbol
        in: path
        required: true
        type: string
    responses:
      200:
        description: Options data
      400:
        description: Error
    """
    try:
        return jsonify(cmd_options(symbol))
    except Exception as e:
        return jsonify({"error": str(e)}), 400


@finance_bp.route('/dividends/<symbol>')
@token_required
def dividends(symbol):
    """Retrieve dividend information and history for a ticker.

    ---
    parameters:
      - name: symbol
        in: path
        required: true
        type: string
    responses:
      200:
        description: Dividend info
      400:
        description: Error
    """
    try:
        return jsonify(cmd_dividends(symbol))
    except Exception as e:
        return jsonify({"error": str(e)}), 400


@finance_bp.route('/ratings/<symbol>')
@token_required
def ratings(symbol):
    """Get analyst ratings and upgrades/downgrades for a ticker.

    ---
    parameters:
      - name: symbol
        in: path
        required: true
        type: string
    responses:
      200:
        description: Ratings data
      400:
        description: Error
    """
    try:
        return jsonify(cmd_ratings(symbol))
    except Exception as e:
        return jsonify({"error": str(e)}), 400


@finance_bp.route('/analysis/<symbol>')
@token_required
def analysis(symbol):
    """Retrieve full analysis and holdings text for a ticker.

    ---
    parameters:
      - name: symbol
        in: path
        required: true
        type: string
    responses:
      200:
        description: Analysis text
      400:
        description: Error
    """
    try:
        text = get_full_analysis_and_holdings_text(symbol.upper())
        return jsonify({"symbol": symbol.upper(), "analysis": text})
    except Exception as e:
        return jsonify({"error": str(e)}), 400

