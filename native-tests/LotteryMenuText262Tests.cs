using System.Runtime.CompilerServices;
using System.Text;
using EquinoxCompanion;

internal static class LotteryMenuText262Tests
{
    [ModuleInitializer] internal static void Run()
    {
        var checks=0;
        void Check(bool ok,string why){checks++;if(!ok)throw new Exception("Lottery text: "+why);}
        unsafe string Read(byte[] bytes){fixed(byte* p=bytes)return LotteryMenuText.Read(p);}
        byte[] Formatted(string text)=>[0x02,0x48,0x04,0xF2,0x02,0x25,0x03,..Encoding.UTF8.GetBytes(text),0x02,0x48,0x02,0x01,0x03,0];
        var prompt="Do you wish to deposit 20,000,000 gil to enter the lottery for this free company plot?";
        Check(Read(Formatted(prompt))==prompt,"formatted purchase purpose becomes plain text");
        var longPrompt=prompt+"\n"+new string('x',3500)+"\nPrivate and free company rules.";
        Check(Read(Formatted(longPrompt))==longPrompt,"long terms do not erase selected purpose");
        Check(Read(Encoding.UTF8.GetBytes("Type of Entry: Free Company Representative\0"))=="Type of Entry: Free Company Representative","plain timer label");
        Check(Read([0])=="","empty");
        Check(Read(Enumerable.Repeat((byte)'x',16384).ToArray())=="","bounded unterminated input");
        unsafe{Check(LotteryMenuText.Read(null)=="","null");}
        Console.WriteLine($"Lottery native text checks passed: {checks}.");
    }
}
