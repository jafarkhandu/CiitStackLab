from pathlib import Path

path = Path("CIITStackLab.WebApp/Areas/Student/Views/Courses/Details.cshtml")
text = path.read_text(encoding="utf-8-sig")

marker = "        (() => {\n            const form = document.getElementById('ciit-demo-payment-form');"
if marker not in text:
    raise SystemExit("Expected payment script marker was not found.")

injection = r'''        (() => {
            const loadRazorpay = () => new Promise((resolve, reject) => {
                if (window.Razorpay) return resolve();
                const existing = document.querySelector('script[src="https://checkout.razorpay.com/v1/checkout.js"]');
                if (existing) {
                    existing.addEventListener('load', resolve, { once: true });
                    existing.addEventListener('error', reject, { once: true });
                    return;
                }
                const script = document.createElement('script');
                script.src = 'https://checkout.razorpay.com/v1/checkout.js';
                script.async = true;
                script.onload = resolve;
                script.onerror = () => reject(new Error('Razorpay Checkout could not be loaded.'));
                document.head.appendChild(script);
            });

            const showGatewayMessage = message => {
                document.getElementById('ciit-razorpay-gateway-message')?.remove();
                const host = document.querySelector('.ciit-payment-modal .modal-body') || document.body;
                const alert = document.createElement('div');
                alert.id = 'ciit-razorpay-gateway-message';
                alert.className = 'alert alert-danger mt-3';
                alert.setAttribute('role', 'alert');
                alert.textContent = message;
                host.prepend(alert);
            };

            window.PhonePeCheckout = {
                transact: async function (options) {
                    try {
                        const checkoutUrl = new URL(options.tokenUrl, window.location.origin);
                        const orderId = checkoutUrl.searchParams.get('razorpayOrderId');
                        const keyId = checkoutUrl.searchParams.get('keyId');
                        const amount = Number(checkoutUrl.searchParams.get('amount'));
                        const merchantOrderId = checkoutUrl.searchParams.get('merchantOrderId');
                        const form = document.getElementById('ciit-demo-payment-form');
                        const token = form?.querySelector('input[name="__RequestVerificationToken"]')?.value;

                        if (!orderId || !keyId || !merchantOrderId || !Number.isSafeInteger(amount) || amount <= 0) {
                            throw new Error('Razorpay order details are incomplete.');
                        }

                        await loadRazorpay();

                        const checkout = new Razorpay({
                            key: keyId,
                            amount,
                            currency: 'INR',
                            order_id: orderId,
                            name: 'CIIT Stack Lab',
                            description: 'Course payment',
                            handler: async function (response) {
                                try {
                                    const verification = await fetch('/Payments/Razorpay/Verify', {
                                        method: 'POST',
                                        credentials: 'same-origin',
                                        headers: {
                                            'Content-Type': 'application/json',
                                            'RequestVerificationToken': token || ''
                                        },
                                        body: JSON.stringify({
                                            merchantOrderId,
                                            razorpayOrderId: response.razorpay_order_id,
                                            razorpayPaymentId: response.razorpay_payment_id,
                                            razorpaySignature: response.razorpay_signature
                                        })
                                    });

                                    const result = await verification.json().catch(() => ({}));
                                    if (!verification.ok || !result.success) {
                                        throw new Error(result.message || 'Payment verification failed.');
                                    }

                                    window.location.assign(result.redirectUrl);
                                } catch (error) {
                                    showGatewayMessage(error instanceof Error ? error.message : 'Payment verification failed.');
                                    if (typeof options.callback === 'function') options.callback('USER_CANCEL');
                                }
                            },
                            modal: {
                                ondismiss: function () {
                                    if (typeof options.callback === 'function') options.callback('USER_CANCEL');
                                }
                            }
                        });

                        checkout.on('payment.failed', function (response) {
                            showGatewayMessage(response?.error?.description || 'Payment failed. Please try again.');
                            if (typeof options.callback === 'function') options.callback('USER_CANCEL');
                        });

                        checkout.open();
                    } catch (error) {
                        showGatewayMessage(error instanceof Error ? error.message : 'Unable to start Razorpay checkout.');
                        if (typeof options.callback === 'function') options.callback('USER_CANCEL');
                    }
                }
            };

            document.addEventListener('DOMContentLoaded', function () {
                document.querySelectorAll('[data-bs-target="#coursePaymentModal"]').forEach(button => {
                    button.innerHTML = '<i class="bi bi-shield-lock-fill me-2"></i>Enroll with Razorpay';
                });
            });
        })();

        (() => {
            const form = document.getElementById('ciit-demo-payment-form');'''

text = text.replace(marker, injection, 1)
path.write_text(text, encoding="utf-8")
