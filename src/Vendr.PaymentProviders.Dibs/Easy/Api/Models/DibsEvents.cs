namespace Vendr.PaymentProviders.Dibs.Easy.Api.Models
{
    public static class DibsEvents
    {
        /// <summary>
        /// Occurs when a Payment has been successfully created.
        /// </summary>
        public const string PaymentCreated = "payment.created";

        /// <summary>
        /// Occurs when a Payment reservation has been successfully created.
        /// </summary>
        public const string PaymentReservationCreated = "payment.reservation.created";

        /// <summary>
        /// Occurs when a Payment reservation has been successfully created (v2).
        /// </summary>
        public const string PaymentReservationCreatedV2 = "payment.reservation.created.v2";

        /// <summary>
        /// Occurs when a Payment Checkout has been successfully completed.
        /// </summary>
        public const string PaymentCheckoutCompleted = "payment.checkout.completed";

        /// <summary>
        /// Occurs when a Payment Charge has been successfully created.
        /// </summary>
        public const string PaymentChargeCreated = "payment.charge.created";

        /// <summary>
        /// Occurs when a Payment Charge has been successfully created (v2).
        /// </summary>
        public const string PaymentChargeCreatedV2 = "payment.charge.created.v2";

        /// <summary>
        /// Occurs when a Payment Charge has failed.
        /// </summary>
        public const string PaymentChargeFailed = "payment.charge.failed";

        /// <summary>
        /// Occurs when a Payment Cancel has been successfully created.
        /// </summary>
        public const string PaymentCancelCreated = "payment.cancel.created";

        /// <summary>
        /// Occurs when a Payment Cancel has failed.
        /// </summary>
        public const string PaymentCancelFailed = "payment.cancel.failed";

        /// <summary>
        /// Occurs when a Payment Refund has been initiated.
        /// </summary>
        public const string PaymentRefundInitiated = "payment.refund.initiated";

        /// <summary>
        /// Occurs when a Payment Refund has been initiated (v2).
        /// </summary>
        public const string PaymentRefundInitiatedV2 = "payment.refund.initiated.v2";

        /// <summary>
        /// Occurs when a Payment Refund has been successfully completed.
        /// </summary>
        public const string PaymentRefundCompleted = "payment.refund.completed";

        /// <summary>
        /// Occurs when a Payment Refund has failed.
        /// </summary>
        public const string PaymentRefundFailed = "payment.refund.failed";
    }
}
