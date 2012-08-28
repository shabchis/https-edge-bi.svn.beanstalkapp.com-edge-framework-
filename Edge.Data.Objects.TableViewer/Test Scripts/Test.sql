DECLARE	@return_value int

EXEC	@return_value = [dbo].[GetDataByAccountID]
		@accountID = 10035,
		@dummyTableName = N'TextCreative',
		@deliveryOutputID = NULL,
		@dateCreated = NULL

SELECT	'Return Value' = @return_value
