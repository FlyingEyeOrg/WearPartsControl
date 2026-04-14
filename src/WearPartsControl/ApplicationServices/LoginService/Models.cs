namespace WearPartsControl.ApplicationServices.LoginService;

public class UserModel
{
    public string card_id { get; set; } = string.Empty;
    public string work_id { get; set; } = string.Empty;
    // Add other properties as needed
}

public class HMRResult
{
    public bool Success { get; set; }
    public HMRData Data { get; set; } = new();
}

public class HMRData
{
    public List<UserModel> list { get; set; } = new();
}