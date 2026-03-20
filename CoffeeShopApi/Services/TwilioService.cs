// Services/TwilioService.cs
using Twilio;
using Twilio.Rest.Api.V2010.Account;
using Twilio.Types;

public class TwilioService
{
    private readonly string? _accountSid;
    private readonly string? _authToken;
    private readonly string? _twilioPhoneNumber;

    public TwilioService(IConfiguration configuration)
    {
        _accountSid = configuration["Twilio:AccountSID"];
        _authToken = configuration["Twilio:AuthToken"];
        _twilioPhoneNumber = configuration["Twilio:FromPhoneNumber"];
        if (!string.IsNullOrEmpty(_accountSid) && !string.IsNullOrEmpty(_authToken))
        {
            TwilioClient.Init(_accountSid, _authToken);
        }
    }

    public async Task SendSmsAsync(string toPhoneNumber, string message)
    {
        if (string.IsNullOrEmpty(_accountSid) || string.IsNullOrEmpty(_authToken) || string.IsNullOrEmpty(_twilioPhoneNumber))
        {
            return;
        }
        var messageOptions = new CreateMessageOptions(new PhoneNumber(toPhoneNumber))
        {
            From = new PhoneNumber(_twilioPhoneNumber),
            Body = message
        };
        await MessageResource.CreateAsync(messageOptions);
    }
}
