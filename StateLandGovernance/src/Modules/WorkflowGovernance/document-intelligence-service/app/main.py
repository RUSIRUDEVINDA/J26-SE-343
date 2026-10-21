"""FastAPI application entrypoint."""

from fastapi import FastAPI, Request, status
from fastapi.exceptions import RequestValidationError
from fastapi.responses import JSONResponse

from app.api.endpoints import router
from app.core.logging import logger
from app.models.schemas import ErrorResponse

app = FastAPI(
    title="Document Intelligence OCR Service",
    description="OCR extraction microservice for State Land Lease Governance (Component 3)",
    version="0.1.0",
    docs_url="/docs",
    redoc_url="/redoc",
)

app.include_router(router)


@app.exception_handler(RequestValidationError)
async def validation_exception_handler(request: Request, exc: RequestValidationError):
    """Sanitize validation error messages without leaking internal structures."""
    errors = exc.errors()
    error_msg = "; ".join(f"{e.get('loc', ['field'])[-1]}: {e.get('msg')}" for e in errors)
    logger.warning(f"Request validation failure on {request.url.path}: {error_msg}")
    return JSONResponse(
        status_code=status.HTTP_400_BAD_REQUEST,
        content=ErrorResponse(
            detail=f"Invalid request parameters: {error_msg}",
            error_code="VALIDATION_ERROR",
        ).model_dump(),
    )


@app.exception_handler(Exception)
async def unhandled_exception_handler(request: Request, exc: Exception):
    """Catch-all exception handler preventing stack trace or machine path leakage."""
    logger.error(f"Unhandled exception on {request.url.path}: {str(exc)}", exc_info=True)
    return JSONResponse(
        status_code=status.HTTP_500_INTERNAL_SERVER_ERROR,
        content=ErrorResponse(
            detail="An unexpected internal error occurred.",
            error_code="INTERNAL_SERVER_ERROR",
        ).model_dump(),
    )
