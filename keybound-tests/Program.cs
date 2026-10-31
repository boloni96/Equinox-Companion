using EquinoxCompanion;
static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
Check(KeyboundPolicy.CanSubmit("Fight On!","Fight On!","Fight On!"),"spaces and punctuation preserved");
Check(!KeyboundPolicy.CanSubmit("new","old","old"),"replaced target cannot be submitted");
Check(!KeyboundPolicy.CanSubmit("word","wor","word"),"partial input cannot be submitted");
Check(!KeyboundPolicy.CanSubmit("","",""),"empty input cannot be submitted");
Check(!KeyboundPolicy.OwnsInput("my message","wor"),"unrelated input is not owned");
Check(!KeyboundPolicy.IsPhrase("???")&&!KeyboundPolicy.IsPhrase("wo*d")&&!KeyboundPolicy.IsPhrase("hello\nchat"),"masked and control text rejected");
Check(KeyboundPolicy.IsPhrase("Chocobo Square"),"multiword phrase accepted");
