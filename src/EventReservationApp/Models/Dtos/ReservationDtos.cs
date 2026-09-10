namespace EventReservationApp.Models.Dtos
{
    public class MyReservationDto
    {
        public int ReservationId { get; set; }
        public int EventId { get; set; }
        public string EventName { get; set; } = string.Empty;
        public DateTime EventStartDate { get; set; }
        public DateTime ReservationDate { get; set; }
        public string? Notes { get; set; }
    }
    public class ReservationOperationResult
    {
        public bool Success { get; set; }
        public string? ErrorMessage { get; set; }
        public int? ReservationId { get; set; }

        public static ReservationOperationResult Ok(int? reservationId = null) =>
            new() { Success = true, ReservationId = reservationId };

        public static ReservationOperationResult Fail(string errorMessage) =>
            new() { Success = false, ErrorMessage = errorMessage };
    }
}
