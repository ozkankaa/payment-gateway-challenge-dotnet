namespace PaymentGateway.Api.Saga.Request
{
    public class PaymentRequestState : PaymentSagaStateMachineInstance
    {
        public Guid? OriginalRequestId { get; set; }
        public Uri? OriginalResponseAddress { get; set; }
    }
}
