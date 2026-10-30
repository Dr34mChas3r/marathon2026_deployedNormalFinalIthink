namespace APIHandler.Entities;

public enum OrderStatus
{
    Created = 0,
    Submitted = 1,
    Cancelled = 2,
    SubmittedByCustomer = 3,
    Preparing = 4,
    Ready = 5,
    Served = 6,
}