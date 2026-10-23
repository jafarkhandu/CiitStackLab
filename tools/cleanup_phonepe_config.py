import json
from pathlib import Path

path = Path("CIITStackLab.WebApp/appsettings.json")
data = json.loads(path.read_text(encoding="utf-8-sig"))
data.pop("PhonePe", None)
data["Razorpay"] = {
    "KeyId": "rzp_test_TrIz5e007DJ61x",
    "KeySecret": "",
    "TimeoutSeconds": 30,
}
path.write_text(json.dumps(data, indent=2) + "\n", encoding="utf-8")
