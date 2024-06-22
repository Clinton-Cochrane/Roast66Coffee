// Services/TwilioService.cs
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

public class TwilioService
{
    private readonly string _accountSid;
    private readonly string _authToken;
    private readonly string _twilioPhoneNumber;

    public TwilioService(IConfiguration configuration)
    {
        _accountSid = configuration["Twilio:AccountSID"];
        _authToken = configuration["Twilio:AuthToken"];
        _twilioPhoneNumber = configuration["Twilio:PhoneNumber"];
        TwilioClient.Init(_accountSid, _authToken);
    }

    public async Task SendSmsAsync(string toPhoneNumber, string message)
    {
        var messageOptions = new CreateMessageOptions(new PhoneNumber(toPhoneNumber))
        {
            From = new PhoneNumber(_twilioPhoneNumber),
            Body = message
        };
        await MessageResource.CreateAsync(messageOptions);
    }
}
