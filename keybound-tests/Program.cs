using EquinoxCompanion;
static void Check(bool ok,string name){if(!ok)throw new Exception(name);Console.WriteLine("PASS "+name);}
Check(KeyboundPolicy.CanSubmit("Fight On!","Fight On!","Fight On!"),"spaces and punctuation preserved");
Check(!KeyboundPolicy.CanSubmit("new","old","old"),"replaced target cannot be submitted");
Check(!KeyboundPolicy.CanSubmit("word","wor","word"),"partial input cannot be submitted");
Check(!KeyboundPolicy.CanSubmit("","",""),"empty input cannot be submitted");
Check(!KeyboundPolicy.OwnsInput("my message","wor"),"unrelated input is not owned");
Check(!KeyboundPolicy.IsPhrase("???")&&!KeyboundPolicy.IsPhrase("wo*d")&&!KeyboundPolicy.IsPhrase("hello\nchat"),"masked and control text rejected");
Check(KeyboundPolicy.IsPhrase("Chocobo Square"),"multiword phrase accepted");

Check(KeyboundPolicy.ShouldRecall("","Cura","Cura",false),"consecutive repeat recalls");
Check(!KeyboundPolicy.ShouldRecall("","Cura","Phoenix",false),"different phrase types normally");
Check(!KeyboundPolicy.ShouldRecall("Cu","Cura","Cura",false),"partial text cannot recall");
Check(!KeyboundPolicy.ShouldRecall("","Cura","Cura",true),"recall attempted only once");
Check(!KeyboundPolicy.CanSubmit("Cura","Phoenix","Cura"),"wrong recalled history never submitted");

Check(KeyboundPolicy.CanRetryInput("Cu","Cu"),"lost key retries from last observed prefix");
Check(KeyboundPolicy.CanRetryInput("","Cu"),"game cleared buffer restarts typing");
Check(!KeyboundPolicy.CanRetryInput("Other","Cu"),"unrelated text cannot be adopted for retry");
Check(!KeyboundPolicy.CanSubmit("Cura","","Cura"),"cleared buffer never submits");

Check(KeyboundPolicy.AbandonMissingTarget(false,"Cur","Cu","Cur"),"vanished target cancels in-flight character acknowledgement");
Check(KeyboundPolicy.AbandonMissingTarget(false,"Cu","Cu","Cur"),"vanished target cancels lost character wait");
Check(!KeyboundPolicy.AbandonMissingTarget(true,"Cu","Cu","Cur"),"live target keeps acknowledgement wait");
Check(!KeyboundPolicy.AbandonMissingTarget(false,"user text","Cu","Cur"),"vanished target cannot erase unrelated text");

Check(KeyboundPolicy.TypingDelay(60,100,0)==60&&KeyboundPolicy.TypingDelay(60,100,1)==100,"configured delay bounds");
Check(KeyboundPolicy.TypingDelay(0,-10,0.5)==10,"invalid saved delay clamped");
Check(KeyboundPolicy.TypingDelay(200,100,0.5)==150,"inverted delay bounds normalized");
Check(KeyboundPolicy.LoopTerritory(1359)&&KeyboundPolicy.LoopTerritory(1360)&&!KeyboundPolicy.LoopTerritory(1),"repeat limited to event territories");

Check(KeyboundPolicy.SoftMinMs==80&&KeyboundPolicy.SoftMaxMs==150&&KeyboundPolicy.SoftSubmitMs==80,"Soft preset matches requested timings");
Check(!KeyboundPolicy.LiveWord(false,true,true,true),"hidden component cannot retain stale live word even with leftover noise overlay");
Check(KeyboundPolicy.LiveWord(true,false,false,true),"visible component with obscuring overlay remains live");
Check(!KeyboundPolicy.LiveWord(true,false,false,false),"disappeared label is not live");
Check(KeyboundPolicy.LiveWord(true,true,true,false),"normal visible word remains live");
Check(KeyboundPolicy.CanClear(true,"Cur","Cur"),"owned nonempty active input can use Escape clear");
Check(!KeyboundPolicy.CanClear(true,"",""),"never Escape an empty input and quit");
Check(!KeyboundPolicy.CanClear(false,"Cur","Cur"),"never Escape inactive input");
Check(!KeyboundPolicy.CanClear(true,"my text","Cur"),"never clear someone else's text");
Check(KeyboundPolicy.TypingDelay(200,100,0)==100&&KeyboundPolicy.TypingDelay(200,100,1)==200,"independent crossed sliders produce bounded sorted timing");

Check(KeyboundPolicy.CanRecoverInput("Cu","Cu","Cur",true),"interrupted pending key can clear its observed buffer");
Check(KeyboundPolicy.CanRecoverInput("C","Cu","Cur",true),"game-truncated pending prefix can recover");
Check(KeyboundPolicy.CanRecoverInput("Curr","Cu","Cur",true),"one duplicated outstanding character can recover");
Check(!KeyboundPolicy.CanRecoverInput("Curx","Cu","Cur",true),"arbitrary appended user text cannot be cleared");
Check(!KeyboundPolicy.CanRecoverInput("Other","Cu","Cur",true),"unrelated text is preserved during recovery");
Check(!KeyboundPolicy.CanRecoverInput("C","Cu","Cur",false),"stale expected text does not grant ownership");
Check(KeyboundPolicy.CanRecoverInput("Cura","Cura","",true),"interrupted submission can clear owned complete word");
Check(KeyboundPolicy.CanRecoverInput("","","Cur",true),"game-cleared field recovers without Escape");
Check(!KeyboundPolicy.CanRecoverInput("Phoenix","","Cura",true),"unexpected history phrase is preserved");
