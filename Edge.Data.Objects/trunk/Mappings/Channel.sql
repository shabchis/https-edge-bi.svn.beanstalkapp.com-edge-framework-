-- # -------------------------------------
-- # TEMPLATE Get

select *
from Channel 
where @channelID = -1 or ID = @channelID;