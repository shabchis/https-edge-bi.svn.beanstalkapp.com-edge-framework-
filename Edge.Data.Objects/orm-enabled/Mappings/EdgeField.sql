-- # -------------------------------------
-- # TEMPLATE GetByID

select top 1
	fields.FieldID,
    fields.AccountID,
    fields.ChannelID,
    fields.Name,
    fields.DisplayName,
    fields.FieldType,
    fields.FieldTypeID,
	types.ClrType as FieldClrType
from
	MD_EdgeField fields
	left outer join MD_EdgeType types on
		types.TypeID = fields.FieldTypeID
where
	FieldID = @fieldID
;

-- # -------------------------------------
-- # TEMPLATE Get

select
	fields.FieldID,
    fields.AccountID,
    fields.ChannelID,
    fields.Name,
    fields.DisplayName,
    fields.FieldType,
    fields.FieldTypeID,
	types.ClrType as FieldClrType
from
	MD_EdgeField fields
	left outer join MD_EdgeType types on
		types.TypeID = fields.FieldTypeID
where
	AccountID in (-1, @accountID) and
	ChannelID in (-1, @channelID)
;