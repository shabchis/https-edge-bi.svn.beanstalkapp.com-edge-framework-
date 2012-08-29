DECLARE	@return_value int

EXEC	@return_value = [dbo].[GetDataByAccountID]
		@accountID = -1,
		@virtualTableName = N'Color',
		@deliveryOutputID = NULL,
		@dateCreated = NULL

SELECT	'Return Value' = @return_value

