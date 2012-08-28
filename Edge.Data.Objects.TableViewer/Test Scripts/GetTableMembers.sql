DECLARE	@return_value int

EXEC	@return_value = [dbo].[GetTableMembers]
		@virtualTableName = N'TextCreative'

SELECT	'Return Value' = @return_value
